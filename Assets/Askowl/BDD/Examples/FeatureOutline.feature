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

  @outline
  Feature: Outlines

  Scenario Outline: eating
    Given there are <start> cucumbers
    When I eat <eat> cucumbers
    Then I should have <left> cucumbers

    Examples:
      | start | eat | left |
      | 12    | 5   | 7    |
      | 20    | 5   | 15   |

  @docString @dataTable
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