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

namespace Askowl.Gherkin {
  /// <a href="https://docs.cucumber.io/gherkin/reference/"></a> //#TBD#//
  [CreateAssetMenu(menuName = "BDD/Definitions", fileName = "Definitions")]
  public class Definitions : Manager {
    [SerializeField] private Vocabulary[] vocabularies       = default;
    [SerializeField] private MonoScript[] gherkinDefinitions = default;

    /// <a href=""></a> //#TBD#//
    [NonSerialized] public bool Success;
    /// <a href=""></a> //#TBD#//
    [NonSerialized] public string Output = "";

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

    protected override void OnEnable() { // Iterate through all the methods of the class.
      base.OnEnable();
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
      vocabulary = vocabularies[0];
      for (int i = 1; i < vocabularies.Length; i++) vocabulary.Merge(vocabularies[i]);

      actions = new Dictionary<Vocabulary.Keywords, Func<Emitter>> {
        {Vocabulary.Keywords.Feature, Feature}
      , {Vocabulary.Keywords.Rule, Rule}
      , {Vocabulary.Keywords.Scenario, Scenario}
      , {Vocabulary.Keywords.Step, Step}
      , {Vocabulary.Keywords.Background, Background}
      , {Vocabulary.Keywords.ScenarioOutline, ScenarioOutline}
      , {Vocabulary.Keywords.Examples, Examples}
      , {Vocabulary.Keywords.DocString, DocString}
      , {Vocabulary.Keywords.DataTable, DataTable}
      , {Vocabulary.Keywords.Comments, Comments}
      , {Vocabulary.Keywords.Tag, Tag}
      , {Vocabulary.Keywords.Unknown, Unknown}
      };

      emitOnComplete = Emitter.Instance;
      process        = Process;
      scenario       = Scenario;
      examples       = Examples;
      outlineActor   = Outline;
      runFiber       = Fiber.Instance.WaitFor(emitOnComplete);
    }
    #endregion

    #region Internal Data
    private struct GherkinLine {
      public          string              text;
      public          string              indent;
      public          string              keyword;
      public          Vocabulary.Keywords state;
      public          bool                colon;
      public          string              statement;
      public          object[]            parameters;
      public override string              ToString() => $"{keyword} ({state}): {statement}";
    }
    // ReSharper disable once CollectionNeverUpdated.Local
    private List<GherkinLine> gherkinLines;
    private StringBuilder     builder;
    private int               lineNumber;
    private GherkinLine       step;
    private RangeInt          background = new RangeInt();
    private RangeInt          outline    = new RangeInt();
    private Emitter           emitOnComplete, emitOnSectionComplete;
    private Fiber             runFiber;
    #endregion

    #region Processing
    /// <a href=""></a> //#TBD#//
    public Fiber Run(string featureFileName) {
      builder = new StringBuilder();
      Success = true;
      if (ReadFile(featureFileName)) Process(0);
      return runFiber.Go();
    }

    private bool ReadFile(string fileName) {
      gherkinLines = new List<GherkinLine>();
      if (!fileName.Contains(".")) fileName += ".feature";
      try {
        using (var file = new StreamReader(fileName)) {
          string text;
          while ((text = file.ReadLine()) != null) {
            var match = gherkinRegex.Match(DropSpaces(text));
            gherkinLines.Add(
              new GherkinLine {
                keyword = match.Groups[2].Value, statement = match.Groups[4].Value
              , indent  = match.Groups[1].Value, colon     = match.Groups[3].Length != 0
              , state   = GherkinSyntax(match.Groups[2].Value, text)
              , text    = text, parameters = new object[3]
              });
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

    private void Process(int from) {
      lineNumber = from - 1;
      Process(Fiber.Instance);
    }

    private void Process(Fiber fiber) {
      Emitter emitter;
      do {
        if (++lineNumber >= gherkinLines.Count) {
          Output = builder.ToString();
          if (Output.Length == 0) Success = false;
          emitOnComplete.Fire();
          return;
        }
        step    = gherkinLines[lineNumber];
        emitter = actions[step.state]();
      } while (emitter == null);
      fiber.Go().WaitFor(emitter).Do(process);
    }
    private Fiber.Action                                   process;
    private Dictionary<Vocabulary.Keywords, Func<Emitter>> actions;
    #endregion

    #region Implementing Gherkin Keywords
    private Emitter Unknown() {
      PrintBaseLine(lineNumber);
      return null;
    }
    private Emitter Feature() {
      background.length = outline.length = 0;
      PrintLine();
      while (!Colon(++lineNumber)) PrintBaseLine(lineNumber);
      return null;
    }

    private Emitter Rule() => Feature();

    private Emitter Background() {
      background = LoadSteps();
      return null;
    }

    private Emitter Scenario() {
      PrintLine();
      RunBackground();
      emitOnSectionComplete = Emitter.SingleFireInstance;
      Scenario(Fiber.Instance);
      return emitOnSectionComplete;
    }
    private void Scenario(Fiber fiber) {
      while (true) {
        if (Colon(lineNumber + 1)) {
          emitOnSectionComplete.Fire();
          return;
        }
        if (gherkinLines[++lineNumber].state == Vocabulary.Keywords.Step) break;
        PrintBaseLine(lineNumber);
      }
      Step();
      fiber.Go().WaitFor(RunStep()).Do(scenario);
    }
    private Fiber.Action scenario;

    private Emitter Step() {
      step = gherkinLines[lineNumber];
      PrintLine();
      var used = DocString(lineNumber) + DataTable(lineNumber);
      for (int i = 1; i < used; i++) PrintBaseLine(lineNumber + i);
      lineNumber += used;
      return null;
    }

    private Emitter DocString() {
      PrintLine();
      return null;
    }

    private Emitter Comments() => null;

    private Emitter ScenarioOutline() {
      outline = LoadSteps();
      return null;
    }

    private Emitter Examples() {
      PrintLine();
      if (!IsDataTable(lineNumber + 1)) {
        Error("Expecting a data table");
        return null;
      }
      if (outline.length == 0) {
        Error("No Scenario Outline Set");
        do {
          lineNumber++;
        } while (IsDataTable(lineNumber));
        return null;
      }
      PrintBaseLine(++lineNumber);
      examplesHeading       = ParseDataTableLine(lineNumber).ToArray();
      emitOnSectionComplete = Emitter.SingleFireInstance;
      Examples(Fiber.Instance);
      return emitOnSectionComplete;
    }

    private void Examples(Fiber fiber) {
      if (!IsDataTable(lineNumber + 1)) {
        emitOnSectionComplete.Fire();
        return;
      }
      PrintBaseLine(++lineNumber);
      examplesEntry            = ParseDataTableLine(lineNumber);
      outlineIndex             = 0;
      emitOnOutlineRowComplete = Emitter.SingleFireInstance;
      Outline(Fiber.Instance);
      fiber.Go().WaitFor(emitOnOutlineRowComplete).Do(examples);
    }

    private void Outline(Fiber fiber) {
      if (outlineIndex >= outline.length) {
        emitOnOutlineRowComplete.Fire();
        return;
      }
      step = gherkinLines[outline.start + outlineIndex];
      var statement = step.statement;
      for (int j = 0; j < examplesHeading.Length; j++) {
        statement = statement.Replace($"<{examplesHeading[j]}>", examplesEntry[j]);
      }
      outlineIndex += DocString(outline.start + outlineIndex) + 1;
      builder.Append("<color=grey>").Append(step.indent).Append(step.keyword).Append(" ").Append(statement)
             .AppendLine("</color>");
      fiber.Go().WaitFor(RunStep(statement)).Do(outlineActor);
    }
    private Fiber.Action examples,        outlineActor;
    private string[]     examplesHeading, examplesEntry;
    private int          outlineIndex;
    private Emitter      emitOnOutlineRowComplete;

    private bool IsDataTable(int at) =>
      (at < gherkinLines.Count) && (gherkinLines[at].state == Vocabulary.Keywords.DataTable);

    private Emitter DataTable() {
      PrintLine();
      Error("Hanging Data Table line");
      return null;
    }

    private Emitter Tag() {
      PrintBaseLine(lineNumber, "red");
      return null;
    }
    #endregion

    #region In support of Gherkin words
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

    private RangeInt LoadSteps() {
      var start = lineNumber + 1;
      PrintLine();
      while (!Colon(++lineNumber)) {
        if (gherkinLines[lineNumber].state == Vocabulary.Keywords.Step) {
          Step();
        } else {
          PrintBaseLine(lineNumber, "grey");
        }
      }
      return new RangeInt(start, lineNumber - start);
    }

    private int DocString(int at) {
      if (++at >= gherkinLines.Count) return 0;
      int first = at;
      if (gherkinLines[at].state != Vocabulary.Keywords.DocString) return 0;
      var left  = gherkinLines[at].indent.Length;
      var start = ++at;
      while (gherkinLines[at].state != Vocabulary.Keywords.DocString) at++;
      string[] docStringLines = new string[at - start];
      for (int i = 0; i < docStringLines.Length; i++) {
        docStringLines[i] = gherkinLines[start + i].text.Substring(left);
      }
      step.parameters[1] = string.Join("\n", docStringLines);
      return at - first;
    }

    private int DataTable(int at) {
      int first = ++at;
      if (!IsDataTable(at)) return 0;
      var table = new List<string[]>();
      while (IsDataTable(at)) table.Add(ParseDataTableLine(at++).ToArray());
      step.parameters[2] = table.ToArray();
      return at - first;
    }

    private string[] ParseDataTableLine(int at) =>
      gherkinLines[at].text.Split('|').Skip(1).Select(s => s.Trim()).ToArray();

    private bool Colon(int lineNo) {
      if (lineNo >= gherkinLines.Count) return true;
      var isColon = gherkinLines[lineNo].colon;
      if (isColon) lineNumber--;
      return isColon;
    }

    private Emitter RunStep(string statement = null) {
      if (statement == null) statement = step.statement;
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
          parameters[i] = step.parameters[1]; // docString
        } else if (type == typeof(string[])) {
          var matches = match.Groups.OfType<Group>().Select(m => m.Value).ToList();
          matches.RemoveAt(0);
          parameters[i] = matches.ToArray();
        } else if (type == typeof(string[][])) {
          parameters[i] = step.parameters[2]; // table
        }
      }
      return parameters;
    }

    private void RunBackground() {
      for (int i = 0; i < background.length; i++) {
        step =  gherkinLines[i];
        i    += DocString(i) + DataTable(i);
        RunStep();
      }
    }
    #endregion

    #region Adding to Output
    private void Error(string message) {
      builder.AppendLine($"{step.indent}<color=red>^^^^^^ {message} ^^^^^^</color>");
      Success = false;
    }

    private void PrintLine() {
      var line = gherkinLines[lineNumber];
      if (line.colon) {
        builder.Append(line.indent).Append("<color=maroon>").Append(line.keyword).Append(":</color> <color=blue>")
               .Append(line.statement).Append("</color>\n");
      } else if (!string.IsNullOrEmpty(line.keyword)) {
        builder.Append(line.indent).Append("<color=blue>").Append(line.keyword).Append("</color> ")
               .AppendLine(line.statement);
      }
    }

    private void PrintBaseLine(int at, string colour = "black") =>
      builder.Append($"<color={colour}>").Append(gherkinLines[at].text).AppendLine("</color>");
    #endregion
  }

  /// <a href=""></a> //#TBD#//
  [AttributeUsage(AttributeTargets.Method)] public class StepAttribute : Attribute {
    private readonly Regex regex;
    public StepAttribute(string definition) => regex = new Regex(definition);

    /// <a href=""></a> //#TBD#//
    public Regex Definition => regex;
  }
}