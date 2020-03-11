Local console
=============

[![Build Status](https://dev.azure.com/MortalFlesh/LocalConsole/_apis/build/status/MortalFlesh.LocalConsole)](https://dev.azure.com/MortalFlesh/LocalConsole/_build/latest?definitionId=1)
[![Build Status](https://api.travis-ci.com/MortalFlesh/local-console.svg?branch=master)](https://travis-ci.com/MortalFlesh/local-console)

## Run statically

First compile
```sh
fake build target release
```

Then run
```sh
dist/local-console help
```

List commands
```sh
dist/local-console list
```

       __                    __       _____                        __
      / /  ___  ____ ___ _  / /      / ___/ ___   ___   ___ ___   / / ___
     / /__/ _ \/ __// _ `/ / /      / /__  / _ \ / _ \ (_-</ _ \ / / / -_)
    /____/\___/\__/ \_,_/ /_/       \___/  \___//_//_//___/\___//_/  \__/


    ==============================================================================

    Usage:
        command [options] [--] [arguments]

    Options:
        -h, --help            Display this help message
        -q, --quiet           Do not output any message
        -V, --version         Display this application version
        -n, --no-interaction  Do not ask any interactive question
        -v|vv|vvv, --verbose  Increase the verbosity of messages

    Available commands:
        about              Displays information about the current project.
        help               Displays help for a command
        list               Lists commands
     repository
        repository:backup  Backup repositories - command will save all repository remote urls to the output file.

---
### Development

First run `dotnet build` or `dotnet watch run`

List commands
```sh
bin/console list
```

Run tests locally
```sh
fake build target Tests
```
