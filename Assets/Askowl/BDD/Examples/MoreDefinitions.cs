// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc
namespace Askowl.Examples {
  public class MoreDefinitions {
    [Test] public void Outline() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/FeatureOutline");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }
    [Test] public void DataTable() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/FeatureDataTable");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }
  }
}
#endif