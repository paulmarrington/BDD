// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages

using System;
using System.Collections.Generic;
using System.IO;
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
    [SerializeField] private Object[] gherkinDefinitions;
    /// <a href=""></a> //#TBD#//
    [SerializeField, Multiline] public string output = default;

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
    }

    /// <a href=""></a> //#TBD#//
    public static Definitions Load(string path) => Load<Definitions>(path);

    /// <a href=""></a> //#TBD#//
    public bool Run(string featureFileName) {
      if (!featureFileName.Contains(".")) featureFileName += ".feature";
      var builder                                         = new StringBuilder();
      var success                                         = true;
      try {
        using (var file = new StreamReader(featureFileName)) {
          string line;
          while ((line = file.ReadLine()) != null) { }
        }
      } catch (Exception e) {
        builder.Append(e);
        success = false;
      } finally {
        output = builder.ToString();
      }
      return success;
    }
  }

  /// <a href=""></a> //#TBD#//
  [Serializable] public class GherkinDefinitions : PlayModeTests { }

  /// <a href=""></a> //#TBD#//
  [AttributeUsage(AttributeTargets.Method)]
  public class StepAttribute : Attribute {
    private readonly Regex regex;
    public StepAttribute(string definition) => regex = new Regex(definition);

    /// <a href=""></a> //#TBD#//
    public Regex Definition => regex;
  }
}