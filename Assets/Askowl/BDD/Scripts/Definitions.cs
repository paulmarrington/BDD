// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CustomAsset;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Askowl.Gherkin {
  /// <a href="https://docs.cucumber.io/gherkin/reference/"></a> //#TBD#//
  [CreateAssetMenu(menuName = "BDD/Definitions", fileName = "Definitions")]
  public class Definitions : Manager, IOnScriptReload {
    [SerializeField] private Vocabulary[] vocabularies       = default;
    [SerializeField] private MonoScript[] gherkinDefinitions = default;

    /// <a href=""></a> //#TBD#//
    public bool Success => errorMessage == null;
    /// <a href=""></a> //#TBD#//
    [NonSerialized] public string errorMessage;

    #region Initialisation
    private Vocabulary vocabulary;

    private struct Definition {
      public Regex           regex;
      public MethodInfo      methodInfo;
      public object          container;
      public ParameterInfo[] parameters;
      public Type[]          parameterTypes;
    }
    private readonly List<Definition> definitionList = new List<Definition>();

    protected override void OnEnable() {
      LoadDefinitions();
      LoadVocabularies();
    }

    private void LoadDefinitions() { // Iterate through all the methods of the class.
      base.OnEnable();
      if (gherkinDefinitions == null) return;
      foreach (MonoScript definitions in gherkinDefinitions) {
        var    type      = definitions.GetClass();
        object container = null;
        foreach (MethodInfo mInfo in type.GetMethods()) {
          foreach (Attribute attr in Attribute.GetCustomAttributes(mInfo)) {
            if (attr.GetType() == typeof(StepAttribute)) {
              var regex                        = ((StepAttribute) attr).Definition;
              if (container == null) container = Activator.CreateInstance(type);
              var parameters                   = mInfo.GetParameters();
              var parameterTypes               = parameters.ToList().Select(m => m.ParameterType).ToArray();
              definitionList.Add(
                new Definition {
                  methodInfo     = mInfo, regex = regex, container = container, parameters = parameters
                , parameterTypes = parameterTypes
                });
            }
          }
        }
      }
    }
    private void LoadVocabularies() {
      vocabulary = vocabularies[0];
      for (int i = 1; i < vocabularies.Length; i++) vocabulary.Merge(vocabularies[i]);
    }
    #endregion

    #region Internal Data
    private struct GherkinStatement {
      public          string                 text;
      public          string                 indent;
      public          string                 keyword;
      public          Vocabulary.Keywords    type;
      public          bool                   colon;
      public          string                 statement;
      public          List<GherkinStatement> extra;
      public override string                 ToString() => $"{keyword} ({type}): {statement}";
    }
    private enum State { Feature, Scenario, Background, Outline, Examples }
    private List<GherkinStatement> gherkinStatements;
    private int                    currentLine, savedLine, endLine, examplesIndex;
    private GherkinStatement       currentStatement;
    private State                  currentState;
    private RangeInt               background, outline;
    private string                 labelToProcess;
    private string[]               activeLabels;
    private string[][]             examples;
    #endregion

    /// <a href=""></a> //#TBD#//
    public Fiber Run(string featureFileName, string label) {
      labelToProcess         = label;
      activeLabels           = new string[0];
      errorMessage           = null;
      Assert.raiseExceptions = true;
      var filePath = Objects.FindFile($"{featureFileName}.feature");
      return (ReadFile(filePath)) ? Process() : null;
    }

    #region Reading Feature File
    private bool ReadFile(string fileName) {
      gherkinStatements = new List<GherkinStatement>();
      try {
        using (var file = new StreamReader(fileName)) {
          string           text;
          bool             inDocString   = false;
          GherkinStatement lastStatement = new GherkinStatement();
          while ((text = file.ReadLine()) != null) {
            var match = gherkinRegex.Match(DropSpacesOrSubCommand(text));
            var statement = new GherkinStatement {
              keyword = match.Groups[2].Value, statement = match.Groups[4].Value
            , indent  = match.Groups[1].Value, colon     = match.Groups[3].Length != 0
            , type    = GherkinSyntax(match.Groups[2].Value, text)
            , text    = text, extra = new List<GherkinStatement>()
            };
            switch (statement.type) {
              case Vocabulary.Keywords.DocString:
                inDocString = !inDocString;
                lastStatement.extra.Add(statement);
                break;
              case Vocabulary.Keywords.DataTable:
                lastStatement.extra.Add(statement);
                break;
              default:
                if (inDocString) {
                  lastStatement.extra.Add(statement);
                  break;
                }
                gherkinStatements.Add(statement);
                if ((statement.type == Vocabulary.Keywords.Step) || (statement.type == Vocabulary.Keywords.Examples)) {
                  lastStatement = statement;
                }
                break;
            }
          }
        }
      } catch (Exception e) { Error(e); }
      return Success;
    }
    private string DropSpacesOrSubCommand(string text) {
      var match = dropSpaceRegex.Match(text);
      if (match.Success)
        if ((vocabulary.Keyword(match.Groups[2].Value) == Vocabulary.Keywords.Step)
         && (vocabulary.Keyword(match.Groups[3].Value) != Vocabulary.Keywords.Unknown))
          text = match.Groups[1].Value + match.Groups[3].Value + " " + match.Groups[4].Value;
        else
          text = match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value + match.Groups[4].Value;
      return text;
    }
    private static readonly Regex gherkinRegex   = new Regex(@"^(\s*)(\w*)(:?)\s*(.*)$");
    private static readonly Regex dropSpaceRegex = new Regex(@"^(\s*)(\w+)\s(\w+):(.*)$");

    private Vocabulary.Keywords GherkinSyntax(string word, string text) {
      var keyword = vocabulary.Keyword(word);
      if (keyword != Vocabulary.Keywords.Unknown) return keyword;

      text = text.TrimStart();
      if (text.Length == 0) return Vocabulary.Keywords.Unknown;
      if (text.StartsWith(@"""""""")) return Vocabulary.Keywords.DocString;
      switch (text[0]) {
        case '|': return Vocabulary.Keywords.DataTable;
        case '@': return Vocabulary.Keywords.Tag;
        case '#': return Vocabulary.Keywords.Comments;
        default:  return Vocabulary.Keywords.Unknown;
      }
    }
    #endregion

    #region Processing Feature File
    private Fiber Process() {
      currentLine  = 0;
      currentState = State.Feature;
      endLine      = -1;
      return Fiber.Start.OnError(Error).Begin
                  .WaitFor(
                     fiber => {
                       do {
                         if (currentLine == endLine) {
                           endLine = -1;
                           if (currentState == State.Examples) {
                             ExamplesRunComplete();
                             if (currentLine >= gherkinStatements.Count) return null;
                           } else {
                             BackgroundRunComplete();
                           }
                         }

                         currentStatement = gherkinStatements[currentLine];

                         switch (currentStatement.type) {
                           case Vocabulary.Keywords.Feature:
                           case Vocabulary.Keywords.Rule:
                             ChangeTo(State.Feature);
                             background.length = outline.length = 0;
                             PrintLine();
                             break;

                           case Vocabulary.Keywords.Scenario:
                             ChangeTo(State.Scenario);
                             PrintLine();
                             RunBackground();
                             break;

                           case Vocabulary.Keywords.Step:
                             if (!Success) {
                               PrintBaseLine("grey");
                               break;
                             }
                             var statement = currentState == State.Examples
                                               ? FillOutlineTemplate()
                                               : currentStatement.statement;
                             PrintLine(statement);
                             if ((currentState != State.Scenario) && (currentState != State.Examples)) break;
                             var emitter = RunStep(statement);
                             if (emitter != null) return emitter;
                             break;

                           case Vocabulary.Keywords.Ask:
                             if (!IsInLabelledSection) break;
                             if (!Success) {
                               PrintBaseLine("grey");
                               break;
                             }
                             PrintLine(currentStatement.statement);
                             var title  = currentStatement.statement;
                             var prompt = DocString();
                             if (prompt.Length == 0) {
                               prompt = title;
                               title  = "BDD";
                             }
                             if (!EditorUtility.DisplayDialog(title, prompt, "Pass", "Fail"))
                               Error("Operator indicated step failure");
                             break;

                           case Vocabulary.Keywords.Background:
                             PrintLine();
                             ChangeTo(State.Background);
                             background.start = currentLine;
                             break;

                           case Vocabulary.Keywords.ScenarioOutline:
                             PrintLine();
                             ChangeTo(State.Outline);
                             outline.start = currentLine;
                             break;

                           case Vocabulary.Keywords.Examples:
                             RunExamples();
                             break;

                           case Vocabulary.Keywords.Tag:
                             PrintBaseLine("brown");
                             activeLabels = gherkinStatements[currentLine].statement.Split();
                             break;

                           case Vocabulary.Keywords.Comments:
                             PrintBaseLine("teal");
                             break;

                           case Vocabulary.Keywords.Unknown:
                             PrintBaseLine("grey");
                             break;
                           default: throw new ArgumentOutOfRangeException();
                         }
                       } while (++currentLine < gherkinStatements.Count);
                       return null;
                     })
                  .Until(_ => ++currentLine >= gherkinStatements.Count);
    }

    private void ChangeTo(State newState) {
      switch (currentState) {
        case State.Background:
          background.length = currentLine - background.start - 1;
          break;
        case State.Outline:
          outline.length = currentLine - outline.start - 1;
          break;
      }
      currentState = newState;
    }
    private void RunBackground() {
      if (background.length == 0) return;
      savedLine   = currentLine + 1;
      currentLine = background.start;
      endLine     = currentLine + background.length;
    }
    private void BackgroundRunComplete() {
      currentLine = savedLine;
      endLine     = -1;
    }

    private void RunExamples() {
      ChangeTo(State.Examples);
      if (outline.length == 0) {
        Error("No scenario outline set");
        return;
      }
      examples      = TableParameter();
      examplesIndex = 1;
      savedLine     = currentLine + 1;
      currentLine   = outline.start;
      endLine       = currentLine + outline.length;
      Log("");
    }
    private void ExamplesRunComplete() {
      if (++examplesIndex >= examples.Length) {
        currentLine = savedLine;
        ChangeTo(State.Feature);
      } else { // next example row
        currentLine = outline.start + 1;
        endLine     = currentLine   + outline.length;
        Log("");
      }
    }
    private string FillOutlineTemplate() {
      var statement = currentStatement.statement;
      for (int j = 0; j < examples[0].Length; j++) {
        statement = statement.Replace($"<{examples[0][j]}>", examples[examplesIndex][j]);
      }
      return statement;
    }
    #endregion

    #region Running a Step
    private Emitter RunStep(string statement = null) {
      if (!IsInLabelledSection) return null;
      if (statement == null) statement = currentStatement.statement;
      for (int i = 0; i < definitionList.Count; i++) {
        var match = definitionList[i].regex.Match(statement);
        if (match.Success) {
          try {
            var parameters = InferParameters(definitionList[i], match);
            return definitionList[i].methodInfo.Invoke(definitionList[i].container, parameters) as Emitter;
          } catch (Exception e) { Error(e); }
        }
      }
      Error("No matching definition");
      return null;
    }
    private object[] InferParameters(Definition definition, Match match) {
      var parameters = new object[definition.parameters.Length];
      for (int i = 0; i < parameters.Length; i++) {
        var type = definition.parameterTypes[i];
        if (type == typeof(string[])) {
          var matches = match.Groups.OfType<Group>().Select(m => m.Value).ToList();
          matches.RemoveAt(0);
          parameters[i] = matches.ToArray();
        } else if (type == typeof(string[][]))
          parameters[i] = TableParameter(); // table
        else if (type == typeof(string)) {
          parameters[i] = DocString();
        } else if (type == typeof(Definitions)) { parameters[i] = this; }
      }
      return parameters;
    }
    private bool IsInLabelledSection =>
      string.IsNullOrWhiteSpace(labelToProcess) || activeLabels.Contains(labelToProcess);

    private string DocString() {
      var extra = currentStatement.extra;

      for (int i = 0; i < extra.Count; i++) {
        if (extra[i].type == Vocabulary.Keywords.DocString) {
          StringBuilder result = new StringBuilder();
          int           start  = extra[i].indent.Length;
          PrintBaseLine(extra[i], "blue");
          while ((++i < extra.Count) && (extra[i].type != Vocabulary.Keywords.DocString)) {
            PrintBaseLine(extra[i], "olive");
            result.AppendLine(extra[i].text.Substring(start));
          }
          if (i < extra.Count) PrintBaseLine(extra[i], "blue");
          return result.ToString();
        }
      }
      return "";
    }
    #endregion

    #region Data Tables
    private string[][] TableParameter() {
      var            extra = currentStatement.extra;
      List<string[]> table = new List<string[]>();

      for (int i = 0; i < extra.Count; i++) {
        if (extra[i].type == Vocabulary.Keywords.DataTable) {
          PrintBaseLine(extra[i], "olive");
          table.Add(ParseDataTableLine(extra[i]));
        }
      }
      return table.ToArray();
    }

    private string[] ParseDataTableLine(GherkinStatement statement) =>
      statement.text.Split('|').Skip(1).Select(s => s.Trim()).ToArray();
    #endregion

    #region Adding to Output
    private void Log(string message) => Debug.Log($"{currentStatement.indent}{message}");

    private void Error(Exception exception) {
      var message = exception.ToString();
      var head    = message.Substring(0, message.IndexOf(Environment.NewLine, StringComparison.Ordinal));
      var body    = message.Substring(head.Length + 1);
      Log($"<color=red>{head}</color>\n<color=maroon>\n{body}</color>");
      errorMessage = exception.ToString();
    }
    private void Error(string message) {
      Log($"<color=red>^^^^^^ {message} ^^^^^^</color>");
      errorMessage = message;
    }

    private void PrintLine(GherkinStatement statement, string text = null) {
      if (!IsInLabelledSection) return;
      if (text == null) text = statement.statement;
      if (statement.colon) {
        Log($"<color=maroon>{statement.keyword}:</color> <color=blue>{text}</color>");
      } else if (!string.IsNullOrEmpty(statement.keyword)) {
        Log($"<color=blue>{statement.keyword}</color> {text}");
      }
    }
    private void PrintLine(string text = null) => PrintLine(currentStatement, text);

    private void PrintBaseLine(GherkinStatement statement, string colour = "black") {
      if (!IsInLabelledSection || (statement.type == Vocabulary.Keywords.Tag)) return;
      Log($"<color={colour}>{statement.text}</color>");
    }
    private void PrintBaseLine(string colour            = "black") => PrintBaseLine(currentLine,           colour);
    private void PrintBaseLine(int    at, string colour = "black") => PrintBaseLine(gherkinStatements[at], colour);
    #endregion

    public void OnScriptReload() => throw new NotImplementedException();
  }

  /// <a href=""></a> //#TBD#//
  [AttributeUsage(AttributeTargets.Method)] public class StepAttribute : Attribute {
    private readonly Regex regex;
    public StepAttribute(string definition) => regex = new Regex(definition);

    /// <a href=""></a> //#TBD#//
    public Regex Definition => regex;
  }

  /// <a href=""></a> //#TBD#//
  public static class Feature {
    /// <a href=""></a> //#TBD#//
    public static Fiber Go(string definitionAsset, string featureFile, string label = "") {
      var definitions = AssetDb.Load<Definitions>($"{definitionAsset}.asset");
      Assert.IsNotNull(definitions, $"Gherkin definitions asset '{definitionAsset}' not found");
      var fiber = Fiber.Instance
                       .WaitFor(_ => definitions.Run(featureFile, label))
                       .Do(_ => Assert.IsTrue(definitions.Success, definitions.errorMessage))
                       .Log("<color=grey>Scenario Processing Complete</color>");
      fiber.Context(definitions);
      return fiber;
    }
  }
}