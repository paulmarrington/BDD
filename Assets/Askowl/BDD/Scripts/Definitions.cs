// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CustomAsset;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Askowl.Gherkin {
  /// <a href="https://docs.cucumber.io/gherkin/reference/"></a> //#TBD#//
  [CreateAssetMenu(menuName = "BDD/Definitions", fileName = "Definitions")]
  public class Definitions : Manager {
    [SerializeField] private Vocabulary[] vocabularies;
    [SerializeField] private Object[]     gherkinDefinitions;

    /// <a href=""></a> //#TBD#//
    [SerializeField, Multiline] public string output = default;

    /// <a href=""></a> //#TBD#//
    [NonSerialized] public bool Success;

    private Vocabulary vocabulary;

    private struct Definition {
      public Regex      regex;
      public MethodInfo methodInfo;
      public Object     container;
    }
    private readonly List<Definition> definitionList = new List<Definition>();

    protected override void OnEnable() { // Iterate through all the methods of the class.
      base.OnEnable();
      foreach (Object definitions in gherkinDefinitions) {
        foreach (MethodInfo mInfo in definitions.GetType().GetMethods()) {
          foreach (Attribute attr in Attribute.GetCustomAttributes(mInfo)) {
            if (attr.GetType() == typeof(StepAttribute)) {
              var regex = ((StepAttribute) attr).Definition;
              definitionList.Add(new Definition {regex = regex, methodInfo = mInfo, container = definitions});
            }
          }
        }
      }
      vocabulary = vocabularies[0];
      for (int i = 1; i < vocabularies.Length; i++) vocabulary.Merge(vocabularies[i]);

      actions = new Dictionary<Vocabulary.Keywords, Action> {
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
    }

    private struct GherkinLine {
      public string              text;
      public string              indent;
      public string              keyword;
      public Vocabulary.Keywords state;
      public bool                colon;
      public string              statement;
      public object[]            parameters;
    }
    private List<GherkinLine> gherkinLines;
    private StringBuilder     builder;
    private int               lineNumber;
    private GherkinLine       gherkinLine, step;
    private RangeInt          docString  = new RangeInt();
    private RangeInt          background = new RangeInt();
    private RangeInt          outline    = new RangeInt();

    /// <a href=""></a> //#TBD#//
    public void Run(string featureFileName) {
      builder = new StringBuilder();
      Success = true;
      if (ReadFile(featureFileName)) Process();
      output = builder.ToString();
    }

    private bool ReadFile(string fileName) {
      gherkinLines = new List<GherkinLine>();
      if (!fileName.Contains(".")) fileName += ".feature";
      try {
        using (var file = new StreamReader(fileName)) {
          string text;
          while ((text = file.ReadLine()) != null) {
            var match = gherkinRegex.Match(text);
            gherkinLines.Append(
              new GherkinLine {
                keyword = match.Groups[2].Value, statement = match.Groups[4].Value
              , indent  = match.Groups[1].Value, colon     = match.Groups[3].Length != 0
              , state   = vocabulary.Keyword(match.Groups[2].Value)
              , text    = text, parameters = new object[2]
              });
          }
        }
      } catch (Exception e) {
        builder.Append("\n<color=red>").Append(e).Append("</color>\n");
        Success = false;
      }
      return Success;
    }
    private static readonly Regex gherkinRegex = new Regex(@"^(\s*)(\w*)(:?)\s*(.*)$");

    private void Process() {
      for (lineNumber = 0; lineNumber < gherkinLines.Count; lineNumber++) {
        gherkinLine = gherkinLines[lineNumber];
        actions[gherkinLine.state]();
      }
    }
    private Dictionary<Vocabulary.Keywords, Action> actions;

    private void Unknown() => PrintBaseLine(lineNumber);

    private void Feature() {
      background.length = outline.length = 0;
      PrintLine(lineNumber);
      while (NotColon(++lineNumber)) PrintBaseLine(lineNumber);
    }
    private void Rule() => Feature();

    private void Background() => LoadSteps(background);

    private void Scenario() {
      PrintLine(lineNumber);
      RunBackground();
      while (NotColon(++lineNumber)) {
        if (gherkinLines[lineNumber].state == Vocabulary.Keywords.Step) {
          Step();
          RunStep();
        } else {
          PrintBaseLine(lineNumber);
        }
      }
    }

    private void Step() {
      step = gherkinLines[lineNumber];
      PrintLine(lineNumber);
      lineNumber += DocString(lineNumber);
    }

    private void DocString() => PrintLine(lineNumber);

    private void Comments() => PrintBaseLine(lineNumber, "silver");

    private void ScenarioOutline() => LoadSteps(outline);

    private void Examples() {
      PrintLine(lineNumber);
      while ((++lineNumber < gherkinLines.Count) && NotColon(lineNumber)) {
        if (gherkinLines[lineNumber].state == Vocabulary.Keywords.DataTable) {
          var headings = ParseDataTableLine();
          while ((++lineNumber < gherkinLines.Count) && NotColon(lineNumber)) {
            if (gherkinLines[lineNumber].state == Vocabulary.Keywords.DataTable) {
              var data = ParseDataTableLine();
              for (int i = 0; i < outline.length; i++) {
                step = gherkinLines[i];
                var statement = step.statement;
                for (int j = 0; j < headings.Count; j++) {
                  statement = statement.Replace($"<{headings[j]}>", data[j]);
                }
                i += DocString(i);
                RunStep(statement);
              }
            }
          }
          break;
        }
      }
    }

    private void DataTable() {
      PrintLine(lineNumber);
      Error("Hanging Data Table line");
    }
    private void Tag() {
      PrintLine(lineNumber);
      Error("Tags are not supported");
    }

    private List<string> ParseDataTableLine() =>
      gherkinLines[lineNumber].text.Split('|').Skip(1).Select(s => s.Trim()).ToList();

    private void LoadSteps(RangeInt to) {
      to.start = lineNumber + 1;
      PrintLine(lineNumber);
      while (NotColon(++lineNumber)) {
        if (gherkinLines[lineNumber].state == Vocabulary.Keywords.Step) {
          Step();
        } else {
          PrintBaseLine(lineNumber, "grey");
        }
      }
      to.length = lineNumber - to.start;
    }

    private int DocString(int at) {
      if (gherkinLines[at + 1].state != Vocabulary.Keywords.DocString) return default;
      PrintLine(at);
      var left = gherkinLines[++at].indent.Length;
      PrintLine(at);
      var start = ++at;
      while (gherkinLines[at].state != Vocabulary.Keywords.DocString) at++;
      string[] docStringLines = new string[at - start];
      for (int i = 0; i < docStringLines.Length; i++) {
        docStringLines[i] = gherkinLines[start + i].text.Substring(left);
        PrintBaseLine(start + i);
      }
      PrintLine(at);
      step.parameters[1] = string.Join("\n", docStringLines);
      return at;
    }

    private bool NotColon(int lineNo) {
      if (lineNo >= gherkinLines.Count) return false;
      var isColon = gherkinLines[lineNo].colon;
      if (isColon) lineNumber--;
      return !isColon;
    }

    private void RunStep(string statement = null) {
      if (statement == null) statement = step.statement;
      for (int i = 0; i < definitionList.Count; i++) {
        var match = definitionList[i].regex.Match(statement);
        if (match.Success) {
          step.parameters[0] = match;
          definitionList[i].methodInfo.Invoke(definitionList[i].container, step.parameters);
          return;
        }
      }
      Error("No matching definition");
    }

    private void RunBackground() {
      for (int i = 0; i < background.length; i++) {
        step =  gherkinLines[i];
        i    += DocString(i);
        RunStep();
      }
    }

    private void Error(string message) =>
      builder.AppendLine($"{step.indent}<color=red>^^^^^^ {message} ^^^^^^</color>");

    private void PrintLine(int at) {
      var line = gherkinLines[at];
      if (line.colon) {
        builder.Append(line.indent).Append("<color=navy>").Append(line.keyword).Append(":</color> <color=blue>")
               .Append(line.statement).Append("</color>\n");
      } else if (!string.IsNullOrEmpty(line.keyword)) {
        builder.Append(line.indent).Append("<color=blue>").Append(line.keyword).Append("</color> ")
               .AppendLine(line.statement);
      }
    }

    private void PrintBaseLine(int at, string colour = "black") =>
      builder.Append($"<color={colour}>").Append(gherkinLines[at].text).AppendLine("</color>");
  }

  /// <a href=""></a> //#TBD#//
  [Serializable] public class GherkinDefinitions : PlayModeTests { }

  /// <a href=""></a> //#TBD#//
  [AttributeUsage(AttributeTargets.Method)] public class StepAttribute : Attribute {
    private readonly Regex regex;
    public StepAttribute(string definition) => regex = new Regex(definition);

    /// <a href=""></a> //#TBD#//
    public Regex Definition => regex;
  }
}