using System.Windows;

namespace LogGrep.ViewModels;

/// <summary>
/// Widths of the data columns, shared by every table of a level. Each grid binds its
/// ColumnDefinition to one of these two-way, so a splitter dragged in one header moves the
/// column in all the rows below it and in the other encounters at the same time.
/// </summary>
public sealed class ColumnLayout : ObservableObject
{
    private static readonly GridLength Fill = new(1, GridUnitType.Star);

    /// <summary>The one instance every grid in the window binds to.</summary>
    public static ColumnLayout Current { get; } = new();

    private GridLength _encounterName = Fill;
    private GridLength _encounterDifficulty = new(180);
    private GridLength _encounterPulls = new(80);
    private GridLength _encounterResult = new(90);
    private GridLength _encounterParty = new(130);
    private GridLength _encounterMistakes = new(110);

    private GridLength _pullResult = new(80);
    private GridLength _pullStart = new(140);
    private GridLength _pullDuration = new(100);
    private GridLength _pullPlayers = new(90);
    private GridLength _pullDps = new(110);
    private GridLength _pullHps = new(110);
    private GridLength _pullMistakes = new(110);

    private GridLength _playerName = new(140);
    private GridLength _playerClass = new(100);
    private GridLength _playerSpec = new(110);
    private GridLength _playerDps = new(85);
    private GridLength _playerHps = new(85);
    private GridLength _playerDtps = new(85);
    private GridLength _playerDied = new(100);
    private GridLength _playerCauses = new(200);

    // The four axes. Narrow on purpose: they hold "63%" and a dash, and everything behind them is
    // a hover away.
    private GridLength _playerOutput = new(52);
    private GridLength _playerSurvival = new(52);
    private GridLength _playerMechanics = new(52);
    private GridLength _playerDuty = new(52);

    private GridLength _playerMistakes = new(230);

    /// <summary>
    /// The lane takes whatever is left, because it is the one column whose job is comparison down
    /// the table rather than reading across a row - and the wider it is, the finer the moments it
    /// can separate.
    /// </summary>
    private GridLength _playerLane = Fill;

    public GridLength EncounterName { get => _encounterName; set => Set(ref _encounterName, value); }
    public GridLength EncounterDifficulty { get => _encounterDifficulty; set => Set(ref _encounterDifficulty, value); }
    public GridLength EncounterPulls { get => _encounterPulls; set => Set(ref _encounterPulls, value); }
    public GridLength EncounterResult { get => _encounterResult; set => Set(ref _encounterResult, value); }
    public GridLength EncounterParty { get => _encounterParty; set => Set(ref _encounterParty, value); }
    public GridLength EncounterMistakes { get => _encounterMistakes; set => Set(ref _encounterMistakes, value); }

    public GridLength PullResult { get => _pullResult; set => Set(ref _pullResult, value); }
    public GridLength PullStart { get => _pullStart; set => Set(ref _pullStart, value); }
    public GridLength PullDuration { get => _pullDuration; set => Set(ref _pullDuration, value); }
    public GridLength PullPlayers { get => _pullPlayers; set => Set(ref _pullPlayers, value); }
    public GridLength PullDps { get => _pullDps; set => Set(ref _pullDps, value); }
    public GridLength PullHps { get => _pullHps; set => Set(ref _pullHps, value); }
    public GridLength PullMistakes { get => _pullMistakes; set => Set(ref _pullMistakes, value); }

    public GridLength PlayerName { get => _playerName; set => Set(ref _playerName, value); }
    public GridLength PlayerClass { get => _playerClass; set => Set(ref _playerClass, value); }
    public GridLength PlayerSpec { get => _playerSpec; set => Set(ref _playerSpec, value); }
    public GridLength PlayerDps { get => _playerDps; set => Set(ref _playerDps, value); }
    public GridLength PlayerHps { get => _playerHps; set => Set(ref _playerHps, value); }
    public GridLength PlayerDtps { get => _playerDtps; set => Set(ref _playerDtps, value); }
    public GridLength PlayerDied { get => _playerDied; set => Set(ref _playerDied, value); }
    public GridLength PlayerCauses { get => _playerCauses; set => Set(ref _playerCauses, value); }
    public GridLength PlayerOutput { get => _playerOutput; set => Set(ref _playerOutput, value); }
    public GridLength PlayerSurvival { get => _playerSurvival; set => Set(ref _playerSurvival, value); }
    public GridLength PlayerMechanics { get => _playerMechanics; set => Set(ref _playerMechanics, value); }
    public GridLength PlayerDuty { get => _playerDuty; set => Set(ref _playerDuty, value); }
    public GridLength PlayerMistakes { get => _playerMistakes; set => Set(ref _playerMistakes, value); }
    public GridLength PlayerLane { get => _playerLane; set => Set(ref _playerLane, value); }
}
