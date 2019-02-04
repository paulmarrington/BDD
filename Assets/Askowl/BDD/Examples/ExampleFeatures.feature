Feature: Guess the word
  The word guess game is a turn-based game for two players.
  The Maker makes a word for the Breaker to guess. The game
  is over when the Breaker guesses the Maker's word.

  # The first example has two steps
  Scenario: Maker starts a game
    When the Maker starts a game
    Then the Maker waits for a Breaker to join

  # The second example has three steps
  Scenario: Breaker joins a game
    Given the Maker has started a game with the word "silky"
    When the Breaker joins the Maker's game
    Then the Breaker must guess a word with 5 characters

Feature: Highlander

  Rule: There can be only One

  Background:
    Given there are 3 ninjas

  Example: Only One -- More than one alive
    Given there are more than one ninjas alive
    When 2 ninjas meet, they will fight
    Then one ninja dies (but not me)
    And there is one ninja less alive

  Example: Only One -- One alive
    Given there is only 1 ninja alive
    Then he (or she) will live forever ;-)

  Rule: There can be Two (in some cases)

  Example: Two -- Dead and Reborn as Phoenix

Feature: Outlines

  Scenario Outline: eating
    Given there are <start> cucumbers
    When I eat <eat> cucumbers
    Then I should have <left> cucumbers

    Examples:
      | start | eat | left |
      | 12    | 5   | 7    |
      | 20    | 5   | 15   |

Feature: Doc Strings and Data Tables

  Example: A Doc String
  Given a blog post named "Random" with Markdown body
  """
  Some Title, Eh?
  ===============
  Here is the first paragraph of my blog post. Lorem ipsum dolor sit amet,
  consectetur adipiscing elit.
  """

  Example: A Data Table
  Given the following users exist:
  | name   | email              | twitter         |
  | Aslak  | aslak@cucumber.io  | @aslak_hellesoy |
  | Julien | julien@cucumber.io | @jbpros         |
  | Matt   | matt@cucumber.io   | @mattwynne      |