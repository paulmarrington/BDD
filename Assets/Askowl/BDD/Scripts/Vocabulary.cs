using System.Collections.Generic;
using System.Linq;
using CustomAsset;
using UnityEngine;
/// <a href=""></a> //#TBD#//
[CreateAssetMenu(menuName = "BDD/Vocabulary", fileName = "Vocabulary")]
public class Vocabulary : Base {
  [SerializeField] private string[] feature         = default;
  [SerializeField] private string[] rule            = default;
  [SerializeField] private string[] scenario        = default;
  [SerializeField] private string[] step            = default;
  [SerializeField] private string[] background      = default;
  [SerializeField] private string[] scenarioOutline = default;
  [SerializeField] private string[] examples        = default;
  [SerializeField] private string[] askAndTell      = default;

  /// <a href=""></a> //#TBD#//
  public enum Keywords {
    // ReSharper disable MissingXmlDoc
    Feature, Rule, Scenario, Step, Background, ScenarioOutline
  , Examples, DocString, DataTable, Tag, Comments, Ask, Unknown
    // ReSharper restore MissingXmlDoc
  }

  private Dictionary<string, Keywords> keywords;

  protected override void OnEnable() {
    keywords = new Dictionary<string, Keywords>();
    void add(string[] keywordList, Keywords keywordType) {
      if (keywordList == null) return;
      for (int i = 0; i < keywordList.Length; i++) {
        keywords[keywordList[i]] = keywordType;
      }
    }
    add(feature,         Keywords.Feature);
    add(rule,            Keywords.Rule);
    add(scenario,        Keywords.Scenario);
    add(step,            Keywords.Step);
    add(background,      Keywords.Background);
    add(scenarioOutline, Keywords.ScenarioOutline);
    add(examples,        Keywords.Examples);
    add(askAndTell,      Keywords.Ask);
  }

  /// <a href=""></a> //#TBD#//
  public void Merge(Vocabulary other) => other.keywords.ToList().ForEach(x => keywords.Add(x.Key, x.Value));

  /// <a href=""></a> //#TBD#//
  public Keywords Keyword(string word) { return keywords.ContainsKey(word) ? keywords[word] : Keywords.Unknown; }
}