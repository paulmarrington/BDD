// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using System.Collections;
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if AskowlTests
// ReSharper disable MissingXmlDoc
namespace Askowl.BDD.Examples {
  public class MoreDefinitions {
    [UnityTest] public IEnumerator Outline() {
      yield return Feature.Go("ExampleDefinitions.asset", "Assets/Askowl/BDD/Examples/FeatureOutline").AsCoroutine();
    }
    [UnityTest] public IEnumerator DataTable() {
      yield return Feature.Go("ExampleDefinitions.asset", "Assets/Askowl/BDD/Examples/FeatureDataTable").AsCoroutine();
    }
    [Step(@"^the following users exist:$")] public void FollowingUsersExist(string[][] table) {
      Assert.AreEqual(4,            table.Length);
      Assert.AreEqual("name",       table[0][0]);
      Assert.AreEqual("@mattwynne", table[3][2]);
    }
    private int cucumbers, eaten;
    [Step(@"^there are (\d+) cucumbers$")] public void ThereAreCucumbers(
      string[] matches) =>
      cucumbers = int.Parse(matches[0]);
    [Step(@"^I eat (\d+) cucumbers$")] public void IEatCucumbers(
      string[] matches) => eaten = int.Parse(matches[0]);
    [Step(@"^I should have (\d+) cucumbers$")] public void CucumbersLeft(
      string[] matches) {
      var left = int.Parse(matches[0]);
      Assert.AreEqual(left, cucumbers - eaten);
    }
  }
}
#endif