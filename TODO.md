- Fix multiple blank line problems in text files (multiple /r/n get collapsed into one)
- Alter the way that queries load data so that LoadColumns is never needed after the table instantiation
- Implement option remove for rating and multiselect fields
- more unit tests (this is complicated by the apparent limitation of not being able to create some field types from the API)
