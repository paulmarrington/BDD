// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
using System;
using Askowl.Gherkin;
using CustomAsset;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR && BDD
// ReSharper disable MissingXmlDoc
namespace Askowl.Examples {
  [Serializable] public class ExampleDefinitions {
    [Test] public void Basic() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/FeatureBasic");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }
    [Test] public void DocString() {
      var definitions = Manager.Load<Definitions>("ExampleDefinitions.asset");
      var results     = definitions.Run("Assets/Askowl/BDD/Examples/FeatureDocString");
      Debug.Log(results);
      Assert.IsTrue(definitions.Success);
    }

    private Emitter joinEmitter;
    private string  wordToGuess;

    [Step(@"^.* Maker starts .+ game$")] public Emitter MakerStartsGame() => null;

    [Step(@"^.* Maker waits for a Breaker to join$")] public Emitter MakerWaitsForBreakerToJoin() =>
      joinEmitter = Emitter.SingleFireInstance;

    [Step(@"^.* Maker has started a game with the word ""(\w+)""$")]
    public Emitter MakerStartsWithWord(string[] matches) {
      wordToGuess = matches[0];
      return null;
    }

    [Step(@"^.* Breaker joins the Maker's game$")] public Emitter BreakerJoinsGame() =>
      throw new NotImplementedException();

    [Step(@"^.* Breaker must guess a word with \d+ characters$")]
    public Emitter GuessWord(string[] matches) => throw new NotImplementedException();
    /*
    [Step(@"^$")] public void (
      string[] matches, string docString, string[][] table) => throw new NotImplementedException();
      */
  }
}
#endif