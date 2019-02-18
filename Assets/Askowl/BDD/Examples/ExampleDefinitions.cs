// Copyright 2019 (C) paul@marrington.net http://www.askowl.net/unity-packages
#if AskowlTests
using System;
using System.Collections;
using Askowl.Gherkin;
using CustomAsset;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.TestTools;

// ReSharper disable MissingXmlDoc
namespace Askowl.BDD.Examples {
  [Serializable] public class ExampleDefinitions {
    [UnityTest] public IEnumerator Basic() {
      yield return Feature.Go("ExampleDefinitions", "FeatureBasic").AsCoroutine();
    }

    [UnityTest] public IEnumerator DocString() {
      yield return Feature.Go("ExampleDefinitions", "FeatureDocString").AsCoroutine();
    }
    [UnityTest] public IEnumerator Background() {
      yield return Feature.Go("ExampleDefinitions", "FeatureBackground").AsCoroutine();
    }

    private string wordToGuess;

    [Step(@"^.* Maker starts .+ game$")] public void MakerStartsGame() { }

    [Step(@"^.* Maker waits for a Breaker to join$")] public void MakerWaitsForBreakerToJoin() { }

    [Step(@"^.* Maker has started a game with the word ""(\w+)""$")]
    public Emitter MakerStartsWithWord(string[] matches) {
      wordToGuess = matches[0];
      return null;
    }

    [Step(@"^.* Breaker joins the Maker's game$")] public Emitter BreakerJoinsGame() => null;

    [Step(@"^.* Breaker must guess a word with (\d+) characters$")]
    public Emitter GuessWord(string[] matches) {
      Assert.AreEqual("silky", wordToGuess);
      Assert.AreEqual("5",     matches[0]);
      var emitter = Emitter.SingleFireInstance;
      Fiber.Start.WaitFor(0.2f).Fire(emitter);
      return emitter;
    }

    [Step(@"^a blog post named ""(.*?)"" with Markdown body$")]
    public void MarkdownBlog(string[] matches, string docString) {
      Assert.AreEqual(1, matches.Length);
      Assert.IsTrue(docString.Contains("Some Title, Eh?"));
    }

    [Step(@"^posted$")] public Emitter Posted() {
      var emitter = Emitter.SingleFireInstance.Context("Posted");
      posted = false;
      Fiber.Start.WaitFor(0.3f).Do(_ => posted = true).Fire(emitter);
      return emitter;
    }
    private bool posted;

    [Step(@"^it is visible as html$")] public void ItIsVisible() => Assert.IsTrue(posted);

    [Step(@"^there are (\d+) ninjas$")]               public void HowManyNinjas(string[] matches) { }
    [Step(@"^there are more than one ninjas alive$")] public void MoreThanOneNinja()              { }
    [Step(@"^2 ninjas meet, they will fight$")]       public void NinjasMeet()                    { }
    [Step(@"^one ninja dies \(but not me\)$")]        public void NinjaDies()                     { }
    [Step(@"^there is one ninja less alive$")]        public void OneNinjaLess()                  { }
    [Step(@"^there is only 1 ninja alive$")]          public void OneNinjaLeft()                  { }
    [Step(@"^he \(or she\) will live forever ;-\)$")] public void LiveForever()                   { }
  }
}
#endif