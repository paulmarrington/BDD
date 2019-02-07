Feature: Doc Strings

  Example: A Doc String
    Given a blog post named "Random" with Markdown body
    """
    Some Title, Eh?
    ===============
    Here is the first paragraph of my blog post. Lorem ipsum dolor sit amet,
    consectetur adipiscing elit.
    """
    When posted
    Then it is visible as html