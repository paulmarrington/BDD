// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc

public class ExampleDefinitions {
  [Test] public void Go() {
    var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
    definitions.Run("Assets/Askowl/BDD/Examples/ExampleFeatures");
    Assert.IsTrue(definitions.Success);
  }
}
#endif