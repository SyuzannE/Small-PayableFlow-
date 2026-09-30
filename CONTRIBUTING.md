# Contributing

1. Fork the repo and create a branch: `git checkout -b feature/short-description`.
2. Make your change and add or update tests in `tests/`.
3. Run `dotnet build` and `dotnet test`. Both must pass.
4. Open a pull request that says what changed and why. Keep pull requests small.

Put business rules in `PayableFlow.Core` and keep the API project thin.
