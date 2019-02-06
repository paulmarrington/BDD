// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using System;
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc
namespace Askowl.Examples {
  [Serializable] public class ExampleDefinitions : Object {
    [Test] public void Basic() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/ExampleFeatures", "@basic");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }
    [Test] public void Outline() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/ExampleFeatures", "@outline");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }
    [Test] public void DataTable() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/ExampleFeatures", "@dataTable");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }

    [Step("^.+ Maker starts .+ game$")] public void MakerStartsGame() {
      Debug.Log($"*** MakerStartsGame"); //#DM#// 
    }
  }
}
#endif