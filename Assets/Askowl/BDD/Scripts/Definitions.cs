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
              var parameterTypes               = parameters.ToList().Select(m => m.GetType()).ToArray();
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
    #endregion

    #region Processing
    /// <a href=""></a> //#TBD#//
    public string Run(string featureFileName) {
      builder = new StringBuilder();
      Success = true;
      if (ReadFile(featureFileName)) Process();
      return builder.ToString();
    }

    private bool ReadFile(string fileName) {
      gherkinLines = new List<GherkinLine>();
      if (!fileName.Contains(".")) fileName += ".feature";
      try {
        using (var file = new StreamReader(fileName)) {
          string text;
          while ((text = file.ReadLine()) != null) {
            var match = gherkinRegex.Match(text);
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
    private static readonly Regex gherkinRegex = new Regex(@"^(\s*)(\w*)(:?)\s*(.*)$");

    private void Process() {
      for (lineNumber = 0; lineNumber < gherkinLines.Count; lineNumber++) {
        step = gherkinLines[lineNumber];
        actions[step.state]();
      }
    }
    private Dictionary<Vocabulary.Keywords, Action> actions;
    #endregion

    #region Implementing Gherkin Keywords
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
      var used = DocString(lineNumber) + DataTable(lineNumber);
      for (int i = 1; i < used; i++) PrintBaseLine(lineNumber + i);
      lineNumber += used;
    }

    private void DocString() => PrintLine(lineNumber);

    private void Comments() => PrintBaseLine(lineNumber, "silver");

    private void ScenarioOutline() => LoadSteps(outline);

    private void Examples() {
      PrintLine(lineNumber);
      if (!IsDataTable(lineNumber + 1)) {
        Error("Expecting a data table");
        return;
      }
      PrintBaseLine(++lineNumber);
      var headings = ParseDataTableLine(lineNumber);
      while (IsDataTable(lineNumber + 1)) {
        PrintBaseLine(++lineNumber);
        var data = ParseDataTableLine(lineNumber);
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

    private bool IsDataTable(int at) =>
      (++at < gherkinLines.Count) && (gherkinLines[at].state == Vocabulary.Keywords.DataTable);

    private void DataTable() {
      PrintLine(lineNumber);
      Error("Hanging Data Table line");
    }

    private void Tag() => PrintBaseLine(lineNumber, "red");
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

    private List<string> ParseDataTableLine(int at) =>
      gherkinLines[at].text.Split('|').Skip(1).Select(s => s.Trim()).ToList();

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
          try {
            var parameters = InferParameters(definitionList[i], match);
            var matches    = match.Groups.OfType<Group>().Select(m => m.Value).ToList();
            matches.RemoveAt(0);
            step.parameters[0] = matches.ToArray();
            definitionList[i].methodInfo.Invoke(definitionList[i].container, parameters);
          } catch (Exception e) {
            Error(e.ToString());
          }
          return;
        }
      }
      Error("No matching definition");
    }

    private object[] InferParameters(Definition definition, Match match) {
      var parameters = new object[definition.parameters.Length];
      for (int i = 0; i < parameters.Length; i++) {
        var type = definition.parameterTypes[i];
        if (type == typeof(string)) {
          parameters[i] = step.parameters[1]; // docString
        } else if (type == typeof(string[])) {
          parameters[i] = match; // matches
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

    private void PrintLine(int at) {
      var line = gherkinLines[at];
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

//  /// <a href=""></a> //#TBD#//
//  [Serializable] public class GherkinDefinitions : MonoBehaviour {
//    [SerializeField] private string description;
//  }

  /// <a href=""></a> //#TBD#//
  [AttributeUsage(AttributeTargets.Method)] public class StepAttribute : Attribute {
    private readonly Regex regex;
    public StepAttribute(string definition) => regex = new Regex(definition);

    /// <a href=""></a> //#TBD#//
    public Regex Definition => regex;
  }
}