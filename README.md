
# SQLite-net Lazy Fork Edition

## New Features

* Foreign Key support (Via the ForeignKeyAttribute).
* Custom Type support (Via SQLiteConfig.AddType, I use it to include NodaTime types in my Table definitions)
* ManyToManyAttribute makes it a little easier to use ForeignKeys, but still needs work, and a tutorial.


## Broken things

* Removed support for DateTimes, StringBuilder and other object types (except for Guid)
* Changed a lot of the API without changing any of the tests, still a TODO.
* TableMappings must be configured before use.
* I probably changed a lot of code that didn't need to be changed, one day maybe I'll distill the good parts of what I have done and try contribute them back to the mothership.


## Source Installation

I broke apart the single source file of the original into classes for my own sanity. This probably means I won't be merging in changes from the the main repo. You can reference the SQLite-net-std project from your own solution, that is all that probably works right now.
