// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using System;
using System.Collections;
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc
namespace Askowl.Examples {
  [Serializable] public class ExampleDefinitions {
    [UnityTest] public IEnumerator Basic() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      yield return definitions.Run("Assets/Askowl/BDD/Examples/FeatureBasic").AsCoroutine();
      Debug.Log(definitions.Output);
      Assert.IsTrue(definitions.Success);
    }

    [UnityTest] public IEnumerator DocString() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      yield return definitions.Run("Assets/Askowl/BDD/Examples/FeatureDocString").AsCoroutine();
      Debug.Log(definitions.Output);
      Assert.IsTrue(definitions.Success);
    }

    private string wordToGuess;

    [Step(@"^.* Maker starts .+ game$")] public void MakerStartsGame() { }

    [Step(@"^.* Maker waits for a Breaker to join$")] public Emitter MakerWaitsForBreakerToJoin() => null;

    [Step(@"^.* Maker has started a game with the word ""(\w+)""$")]
    public Emitter MakerStartsWithWord(string[] matches) {
      wordToGuess = matches[0];
      return null;
    }

    [Step(@"^.* Breaker joins the Maker's game$")] public Emitter BreakerJoinsGame() => null;

    [Step(@"^.* Breaker must guess a word with \d+ characters$")]
    public Emitter GuessWord(string[] matches) {
      Assert.AreEqual("silky", wordToGuess);
      return null;
    }
    /*
    [Step(@"^$")] public void (
      string[] matches, string docString, string[][] table) => throw new NotImplementedException();
      */
  }
}
#endif