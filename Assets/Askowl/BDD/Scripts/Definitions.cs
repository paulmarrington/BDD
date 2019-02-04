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
      public MemberInfo memberInfo;
    }
    private List<Definition> list = new List<Definition>();

    protected override void OnEnable() { // Iterate through all the methods of the class.
      base.OnEnable();
      foreach (Object definitions in gherkinDefinitions) {
        foreach (MethodInfo mInfo in definitions.GetType().GetMethods()) {
          foreach (Attribute attr in Attribute.GetCustomAttributes(mInfo)) {
            if (attr.GetType() == typeof(StepAttribute)) {
              list.Add(new Definition {regex = ((StepAttribute) attr).Definition, memberInfo = mInfo});
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

    struct Line {
      public string              text;
      public string              indent;
      public string              keyword;
      public Vocabulary.Keywords state;
      public bool                colon;
      public string              statement;
    }
    private List<Line>          lines;
    private StringBuilder       builder;
    private Vocabulary.Keywords state;
    private int                 lineNumber;
    private Line                line;
    private string              step;
    private int                 docStringStart, docStringEnd;
    private string              currentColour = "black";

    /// <a href=""></a> //#TBD#//
    public void Run(string featureFileName) {
      builder = new StringBuilder();
      Success = true;
      if (ReadFile(featureFileName)) Process();
      output = builder.ToString();
    }

    private bool ReadFile(string name) {
      lines = new List<Line>();
      if (!name.Contains(".")) name += ".feature";
      try {
        using (var file = new StreamReader(name)) {
          string text;
          while ((text = file.ReadLine()) != null) {
            var match = gherkinRegex.Match(text);
            lines.Append(
              new Line {
                keyword = match.Groups[2].Value, statement = match.Groups[4].Value
              , indent  = match.Groups[1].Value, colon     = match.Groups[3].Length != 0, text = text
              , state   = vocabulary.Keyword(match.Groups[2].Value)
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
      state = Vocabulary.Keywords.Unknown;
      for (lineNumber = 0; lineNumber < lines.Count; lineNumber++) {
        line = lines[lineNumber];
        actions[line.state]();
      }
    }

    private Dictionary<Vocabulary.Keywords, Action> actions;

    private void ChangeState() {
      if (line.colon) {
        FillStep();
        state = line.state;
        PrintLine();
      } else {
        PrintBaseLine();
      }
    }

    private void Feature() => ChangeState();
    private void Rule()    => ChangeState();

    private void Scenario() {
      if (line.colon) {
        FillStep();
        ChangeState();
      } else if (!string.IsNullOrEmpty(line.keyword)) {
        Step();
      } else {
        PrintBaseLine();
      }
    }
    private void Step() {
      FillStep();
      state = line.state;
      step  = line.statement;
    }
    private void Background()      { }
    private void ScenarioOutline() { }
    private void Examples()        { }
    private void DocString()       { }
    private void DataTable()       { }
    private void Tag()             { }
    private void Comments()        { }
    private void Unknown()         { }

    private void FillStep() {
      string docString = default;
      if (InDocString()) {
        var      left           = lines[docStringStart].indent.Length;
        string[] docStringLines = new string[docStringEnd - docStringStart - 2];
        for (int i = 0; i < docStringLines.Length; i++) {
          docStringLines[i] = lines[docStringStart + i + 1].text.Substring(left);
        }
        docString = string.Join("\n", docStringLines);
      }
      Debug.Log($"*** FillStep '{step}' with '{docString}'"); //#DM#// 
    }

    private void PrintLine() {
      if (line.colon) {
        builder.Append(line.indent).Append("<color=navy>").Append(line.keyword).Append(":</color> <color=blue>")
               .Append(line.statement).Append("</color>\n");
      } else if (!string.IsNullOrEmpty(line.keyword)) {
        builder.Append(line.indent).Append("<color=blue>").Append(line.keyword).Append("</color> ")
               .AppendLine(line.statement);
      }
    }

    private void PrintBaseLine() =>
      builder.Append($"<color={currentColour}>").Append(line.text).AppendLine("</color>");

    private bool InDocString() => (docStringEnd - docStringEnd) >= 2;
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