// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using System.Collections;
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc
namespace Askowl.Examples {
  public class MoreDefinitions {
    [UnityTest] public IEnumerator Outline() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      yield return definitions.Run("Assets/Askowl/BDD/Examples/FeatureOutline").AsCoroutine();
      Debug.Log(definitions.Output);
      Assert.IsTrue(definitions.Success);
    }
    [UnityTest] public IEnumerator DataTable() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      yield return definitions.Run("Assets/Askowl/BDD/Examples/FeatureDataTable").AsCoroutine();
      Debug.Log(definitions.Output);
      Assert.IsTrue(definitions.Success);
    }
    [Step(@"^the following users exist:$")] public void FollowingUsersExist(string[][] table) {
      Assert.AreEqual(4,            table.Length);
      Assert.AreEqual("name",       table[0][0]);
      Assert.AreEqual("@mattwynne", table[3][2]);
    }
    /*
    [Step(@"^$")] public void (
      string[] matches, string docString, string[][] table) => throw new NotImplementedException();
      */
  }
}
#endif