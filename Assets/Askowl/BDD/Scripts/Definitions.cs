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
  public class Definitions : Manager {
    [SerializeField] private Vocabulary[] vocabularies       = default;
    [SerializeField] private MonoScript[] gherkinDefinitions = default;

    /// <a href=""></a> //#TBD#//
    [NonSerialized] public bool Success;

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
      public          object[]               parameters;
      public          List<GherkinStatement> extra;
      public override string                 ToString() => $"{keyword} ({type}): {statement}";
    }
    private enum State { Feature, Scenario, Background, Outline, Step, RunningBackground }
    private          List<GherkinStatement> gherkinStatements;
    private readonly StringBuilder          builder = new StringBuilder();
    private          int                    currentLine, savedLine, endLine, examplesIndex;
    private          GherkinStatement       currentStatement;
    private          State                  currentState, lastState;
    private          RangeInt               background,   outline;
    private          string                 activeLabels, labelToProcess;
    private          bool                   inOutline;
    #endregion

    /// <a href=""></a> //#TBD#//
    public Fiber Run(string featureFileName, string label) {
      labelToProcess = label;
      activeLabels   = "";
      builder.Clear();
      Success = true;
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
            var match = gherkinRegex.Match(DropSpaces(text));
            var statement = new GherkinStatement {
              keyword = match.Groups[2].Value, statement = match.Groups[4].Value
            , indent  = match.Groups[1].Value, colon     = match.Groups[3].Length != 0
            , type    = GherkinSyntax(match.Groups[2].Value, text)
            , text    = text, parameters = new object[3], extra = default
            };
            switch (statement.type) {
              case Vocabulary.Keywords.DocString:
                if (inDocString) {
                  inDocString = false;
                  break;
                }
                inDocString         = true;
                lastStatement.extra = new List<GherkinStatement>();
                lastStatement.extra.Add(statement);
                break;
              case Vocabulary.Keywords.DataTable:
                if (lastStatement.extra == default) lastStatement.extra = new List<GherkinStatement>();
                lastStatement.extra.Add(statement);
                break;
              default:
                if (inDocString) {
                  lastStatement.extra.Add(statement);
                } else {
                  gherkinStatements.Add(statement);
                }
                if ((statement.type == Vocabulary.Keywords.Step) || (statement.type == Vocabulary.Keywords.Examples)) {
                  lastStatement = statement;
                }
                break;
            }
          }
        }
      } catch (Exception e) {
        builder.Append("\n<color=red>").Append(e).Append("</color>\n");
        Success = false;
      }
      return Success;
    }
    private string DropSpaces(string text) {
      var match               = dropSpaceRegex.Match((text));
      if (match.Success) text = match.Groups[1].Value + match.Groups[2].Value;
      return text;
    }
    private static readonly Regex gherkinRegex   = new Regex(@"^(\s*)(\w*)(:?)\s*(.*)$");
    private static readonly Regex dropSpaceRegex = new Regex(@"^(\s*\w+)\s(\w+:.*)$");

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
      currentState = lastState = State.Feature;
      inOutline = false;
      return Fiber.Start.Begin
                  .WaitFor(
                     fiber => {
                       do {
                         if (currentLine == endLine) { BackgroundRunComplete(); }

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
                             PrintLine();
                             if (currentState != State.Feature) break;
                             var emitter = RunStep();
                             if (emitter != null) return emitter;
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
                             PrintBaseLine("red");
                             activeLabels = gherkinStatements[currentLine].statement;
                             break;

                           case Vocabulary.Keywords.Comments:
                             PrintBaseLine("gray");
                             break;

                           case Vocabulary.Keywords.Unknown:
                             PrintBaseLine("silver");
                             break;
                           default: throw new ArgumentOutOfRangeException();
                         }
                       } while (++currentLine < gherkinStatements.Count);
                       return null;
                     })
                  .Until(_ => ++currentLine >= gherkinStatements.Count);
    }

    private void ChangeTo(State newState) {
      switch (lastState) {
        case State.Background:
          background.length = currentLine - background.start - 1;
          break;
        case State.Outline:
          outline.length = currentLine - outline.start - 1;
          break;
      }
      lastState    = currentState;
      currentState = newState;
    }
    private void RunBackground() {
      savedLine   = currentLine;
      currentLine = background.start;
      endLine     = currentLine + background.length;
    }
    private void BackgroundRunComplete() => currentLine = savedLine;

    private void RunExamples() {
      inOutline = true;
      examplesIndex = 0;
      savedLine   = currentLine;
      currentLine = outline.start;
      endLine     = currentLine + outline.length;
    }
    private void ExamplesRunComplete() {
      var examples = gherkinStatements[savedLine];
    }
    #endregion

    #region Running a Step
    private Emitter RunStep(string statement = null) {
      if (!isInLabelledSection) return null;
      if (statement == null) statement = currentStatement.statement;
      for (int i = 0; i < definitionList.Count; i++) {
        var match = definitionList[i].regex.Match(statement);
        if (match.Success) {
          try {
            var parameters = InferParameters(definitionList[i], match);
            return definitionList[i].methodInfo.Invoke(definitionList[i].container, parameters) as Emitter;
          } catch (Exception e) {
            Error(e.ToString());
          }
        }
      }
      Error("No matching definition");
      return null;
    }
    private object[] InferParameters(Definition definition, Match match) {
      var parameters = new object[definition.parameters.Length];
      for (int i = 0; i < parameters.Length; i++) {
        var type = definition.parameterTypes[i];
        if (type == typeof(string)) {
          parameters[i] = currentStatement.parameters[1]; // docString
        } else if (type == typeof(string[])) {
          var matches = match.Groups.OfType<Group>().Select(m => m.Value).ToList();
          matches.RemoveAt(0);
          parameters[i] = matches.ToArray();
        } else if (type == typeof(string[][])) {
          parameters[i] = currentStatement.parameters[2]; // table
        }
      }
      return parameters;
    }
    private bool isInLabelledSection =>
      string.IsNullOrWhiteSpace(labelToProcess) || activeLabels.Contains(labelToProcess);
    #endregion

    // ********** OLD **********

    private Emitter Examples() {
      PrintLine();
      if (!IsDataTable(currentLine + 1)) {
        Error("Expecting a data table");
        return null;
      }
      if (outline.length == 0) {
        Error("No Scenario Outline Set");
        do {
          currentLine++;
        } while (IsDataTable(currentLine));
        return null;
      }
      PrintBaseLine(++currentLine);
      examplesHeading       = ParseDataTableLine(currentLine).ToArray();
      emitOnSectionComplete = Emitter.SingleFireInstance;
      Examples(Fiber.Instance);
      return emitOnSectionComplete;
    }

    private void Examples(Fiber fiber) {
      if (!IsDataTable(currentLine + 1)) {
        emitOnSectionComplete.Fire();
        return;
      }
      PrintBaseLine(++currentLine);
      examplesEntry            = ParseDataTableLine(currentLine);
      examplesIndex             = 0;
      emitOnOutlineRowComplete = Emitter.SingleFireInstance;
      Outline(Fiber.Instance);
      fiber.Go().WaitFor(emitOnOutlineRowComplete).Do(examples);
    }

    private void Outline(Fiber fiber) {
      if (examplesIndex >= outline.length) {
        emitOnOutlineRowComplete.Fire();
        return;
      }
      currentStatement = gherkinStatements[outline.start + examplesIndex];
      var statement = currentStatement.statement;
      for (int j = 0; j < examplesHeading.Length; j++) {
        statement = statement.Replace($"<{examplesHeading[j]}>", examplesEntry[j]);
      }
      examplesIndex += DocString(outline.start + examplesIndex) + 1;
      builder.Append("<color=grey>").Append(currentStatement.indent).Append(currentStatement.keyword).Append(" ")
             .Append(statement)
             .AppendLine("</color>");
      fiber.Go().WaitFor(RunStep(statement)).Do(outlineActor);
    }
    private Fiber.Action examples,        outlineActor;
    private string[]     examplesHeading, examplesEntry;
//    private int          outlineIndex;
    private Emitter      emitOnOutlineRowComplete;

    private bool IsDataTable(int at) =>
      (at < gherkinStatements.Count) && (gherkinStatements[at].type == Vocabulary.Keywords.DataTable);

    private Emitter DataTable() {
      PrintLine();
      Error("Hanging Data Table line");
      return null;
    }

//    private Emitter Tag() {
//      PrintBaseLine(currentLine, "red");
//      currentLabels = gherkinStatements[currentLine].statement;
//      return null;
//    }
//    private string currentLabels;
//    private string labelToProcess;
    #endregion

    #region In support of Gherkin words
    private RangeInt LoadSteps() {
      var start = currentLine + 1;
      PrintLine();
      while (!EndSteps(++currentLine)) {
        if (gherkinStatements[currentLine].type == Vocabulary.Keywords.Step) {
          Step();
        } else {
          PrintBaseLine(currentLine, "grey");
        }
      }
      return new RangeInt(start, currentLine - start);
    }

    private int DocString(int at) {
      if (++at >= gherkinStatements.Count) return 0;
      int first = at;
      if (gherkinStatements[at].type != Vocabulary.Keywords.DocString) return 0;
      var left  = gherkinStatements[at].indent.Length;
      var start = ++at;
      while (gherkinStatements[at].type != Vocabulary.Keywords.DocString) at++;
      string[] docStringLines = new string[at - start];
      for (int i = 0; i < docStringLines.Length; i++) {
        docStringLines[i] = gherkinStatements[start + i].text.Substring(left);
      }
      currentStatement.parameters[1] = string.Join("\n", docStringLines);
      return at - first;
    }

    private int DataTable(int at) {
      int first = ++at;
      if (!IsDataTable(at)) return 0;
      var table = new List<string[]>();
      while (IsDataTable(at)) table.Add(ParseDataTableLine(at++).ToArray());
      currentStatement.parameters[2] = table.ToArray();
      return at - first;
    }

    private string[] ParseDataTableLine(int at) =>
      gherkinStatements[at].text.Split('|').Skip(1).Select(s => s.Trim()).ToArray();

    private bool EndSteps(int lineNo) {
      if (lineNo >= gherkinStatements.Count) return true;
      var isColon = gherkinStatements[lineNo].colon || (gherkinStatements[lineNo].type == Vocabulary.Keywords.Tag);
      if (isColon) currentLine--;
      return isColon;
    }
    #endregion

    #region Adding to Output
    /// <a href=""></a> //#TBD#//
    public string Output => builder.ToString();

    private void Error(string message) {
      builder.AppendLine($"{currentStatement.indent}<color=red>^^^^^^ {message} ^^^^^^</color>");
      Success = false;
    }

    private void PrintLine() {
      if (!isInLabelledSection) return;
      var line = gherkinStatements[currentLine];
      if (line.colon) {
        builder.Append(line.indent).Append("<color=maroon>").Append(line.keyword).Append(":</color> <color=blue>")
               .Append(line.statement).Append("</color>\n");
      } else if (!string.IsNullOrEmpty(line.keyword)) {
        builder.Append(line.indent).Append("<color=blue>").Append(line.keyword).Append("</color> ")
               .AppendLine(line.statement);
      }
    }

    private void PrintBaseLine(string colour = "black") => PrintBaseLine(currentLine, colour);

    private void PrintBaseLine(int at, string colour = "black") {
      if (!isInLabelledSection || (gherkinStatements[at].type == Vocabulary.Keywords.Tag)) return;
      builder.Append($"<color={colour}>").Append(gherkinStatements[at].text).AppendLine("</color>");
    }
    #endregion
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
      var definitions = Manager.Load<Definitions>($"{definitionAsset}.asset");
      var fiber = Fiber.Start.WaitFor(definitions.Run(featureFile, label)).Do(
        _ => {
          Debug.Log($"*** Go 'DONE'"); //#DM#//
          Debug.Log(definitions.Output);
          Assert.IsTrue(definitions.Success, "Failure in this Gherkin feature file");
        });
      fiber.Context(definitions);
      return fiber;
    }
  }
}