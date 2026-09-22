# Seety

A single strip over the Cities: Skylines II HUD that shows the state of your city — and where
**every entry opens what it is about**.

Click a reading and the matching in-game info view opens. Click one that has more behind it and a
window appears: the city's problems grouped by type with the camera jumping to each, the workforce
against the jobs that exist, who lives here by age and education, why zone demand is stuck, which
school is about to overflow, which cemetery is about to fill.

Use the gear to enter configuration mode to choose readings and move the bar. Outside that
mode the bar stays locked in place.

In the Traffic window, choose **Selected journey** and select a citizen or vehicle in the game.
The panel shows its current street or transport line, destination and remaining route. Click a
transport line in the route to open its game panel; choose **Show traffic jams** to return to
the congestion list. Journey tracking stops when the window closes.

## What it shows

Twenty-six readings, each switched on or off by clicking the thing itself rather than a
checkbox: happiness, health, unemployment, homelessness, workers, tourists, active problems,
electricity, water delivered, sewage taken away, crematorium capacity, cemetery space, four school
levels, parking, garbage, safety, fire safety, environment quality, post, traffic, land value, zone
demand and transport.

Counted as rows on the bar. Several carry more behind them than they show: environment quality is
one row with four kinds of pollution behind it; health also holds hospital coverage, garbage also
holds landfill space, and safety also holds free cells; electricity holds the battery charge and
the power the city buys and sells; water and sewage hold their own trade.

Available in the twelve languages the game supports.

Two rules decide what is in that list:

1. **It must not already be on the vanilla HUD.** Population, money and income are on screen
   permanently, so repeating them buys nothing and costs width.
2. **It must mirror the exact source vanilla uses for the same figure.** A number that disagrees
   with the game's own panels is worse than no number at all.

## Design

**An alert is just a metric with a threshold.** A count is a statistic; the same count with a line
drawn under it is a warning. Treating them as one idea is why this needs one bar and one list
rather than two of each.

**More is always better.** Half of what a city reports is naturally negative — pollution, crime,
cells occupied. Those are inverted and renamed, so a fuller, greener bar always means better and
there is no direction to remember per icon.

**It reads, and it takes you there.** The bar itself writes nothing. The only things clicking a
reading changes are which info view is active and where the camera is looking. The one exception
lives on the options page, not the bar: a field to add or remove treasury funds, typed in on
purpose and confirmed before it does anything.

[`DESIGN.md`](DESIGN.md) has the reasoning behind each decision, including the traps found along
the way.

## Building

```bash
cd UI && npm install     # once
cd UI && npm run build   # whenever the UI changes
dotnet build Seety.sln -c Release
```

The C# build copies `UI/dist` into the output before deploying, so one `dotnet build` produces the
finished package — provided the UI has been built at least once.

Requires the official Cities: Skylines II modding toolchain. `ModPostProcessor` targets .NET 6 with
no roll-forward, so on a machine with only .NET 8 installed set `DOTNET_ROLL_FORWARD=Major` for the
build.

## Licence

MIT — see [`LICENSE`](LICENSE).
