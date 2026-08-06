using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.Services;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.API.Services;

public class BlackjackService : IServiceSingleton
{
    private readonly ConcurrentDictionary<ulong, BlackjackTable> _tables = new();
    private readonly Global _global;
    private readonly UserAccounts _userAccounts;
    private readonly List<WordCategory> _wordCategories;
    private readonly Timer _cleanupTimer;

    public BlackjackService(Global global, UserAccounts userAccounts)
    {
        _global = global;
        _userAccounts = userAccounts;

        // Load word list
        var wordsPath = Path.Combine(AppContext.BaseDirectory, "DataBase", "blackjack_words.json");
        if (File.Exists(wordsPath))
        {
            var json = File.ReadAllText(wordsPath);
            var doc = JsonSerializer.Deserialize<WordListFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            _wordCategories = doc?.Categories ?? new List<WordCategory>();
        }
        else
        {
            _wordCategories = new List<WordCategory>();
            Console.WriteLine($"[Blackjack] WARNING: Word list not found at {wordsPath}");
        }

        // Cleanup stale tables every 5 minutes
        _cleanupTimer = new Timer
        {
            AutoReset = true,
            Interval = 300_000,
            Enabled = true,
        };
        _cleanupTimer.Elapsed += (_, _) => CleanupStaleTables();
    }

    // ── Public API ─────────────────────────────────────────────────────

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Seats a real human who has just entered the Shinigami world so the final-round client
    /// can still join after the parent game leaves GamesList. For the Ranked creator it also
    /// persists the one-time debit before arming exactly one possible recovery result.
    /// </summary>
    public bool RegisterShinigamiArrival(GameClass game, GamePlayerBridgeClass player)
    {
        if (game == null
            || player == null
            || player.IsBot()
            || !player.Passives.IsDead
            || player.Passives.DeathSource != "Kira")
            return false;

        BlackjackTable table;
        BlackjackPlayer blackjackPlayer;
        while (true)
        {
            table = _tables.GetOrAdd(game.GameId, _ => new BlackjackTable());
            lock (table)
            {
                if (!IsOpenTable(game.GameId, table))
                    continue;

                blackjackPlayer = table.Players.FirstOrDefault(candidate =>
                    candidate.DiscordId == player.DiscordId);
                if (blackjackPlayer == null)
                {
                    if (table.Players.Count >= 5)
                        return false;
                    blackjackPlayer = CreatePlayer(player.DiscordId, player.DiscordUsername);
                    table.Players.Add(blackjackPlayer);
                }
                table.LastActivity = DateTime.UtcNow;
                break;
            }
        }

        if (!game.IsRanked || player.DiscordId != game.CreatorId)
            return false;

        var account = _userAccounts.GetAccount(player.DiscordId);
        if (account == null)
            return false;

        lock (game)
        {
            if (game.RankedShinigamiPenaltyApplied)
                return false;

            int ratingBeforePenalty;
            lock (account)
            {
                ratingBeforePenalty = account.EloRating;
                game.RankedEloRatingAtStart ??= ratingBeforePenalty;
                account.EloRating += RankedElo.ShinigamiPenalty;
                if (!_userAccounts.SaveAccount(account))
                {
                    Console.WriteLine(
                        $"[Blackjack] Ranked Shinigami penalty for {player.DiscordId} is retained in memory for a save retry.");
                }
            }

            game.RankedShinigamiPenaltyApplied = true;

            lock (table)
            {
                table.RankedGame = game;
                blackjackPlayer.RankedEloRecoveryEligible = true;
                blackjackPlayer.RankedEloRatingBeforePenalty = ratingBeforePenalty;
                table.LastActivity = DateTime.UtcNow;
            }
        }

        return true;
    }

    public (BlackjackTableStateDto state, string error) JoinTable(ulong gameId, ulong discordId, string username)
    {
        while (true)
        {
            if (!_tables.TryGetValue(gameId, out var table))
            {
                var game = FindActiveGame(gameId);
                var player = game?.PlayersList.FirstOrDefault(candidate => candidate.DiscordId == discordId);
                if (player == null || !player.Passives.IsDead || player.Passives.DeathSource != "Kira")
                    return (null, GameLocalization.MessageForUser(discordId, "kotgh.blackjack.unavailable"));

                table = _tables.GetOrAdd(gameId, _ => new BlackjackTable());
            }

            lock (table)
            {
                if (!IsOpenTable(gameId, table))
                    continue;

                // Already seated?
                var existing = table.Players.FirstOrDefault(p => p.DiscordId == discordId);
                if (existing != null)
                    return (ToDto(gameId, table, discordId), null);

                var activeGame = FindActiveGame(gameId);
                var activePlayer = activeGame?.PlayersList.FirstOrDefault(candidate => candidate.DiscordId == discordId);
                if (activePlayer == null
                    || !activePlayer.Passives.IsDead
                    || activePlayer.Passives.DeathSource != "Kira")
                    return (null, GameLocalization.MessageForUser(discordId, "kotgh.blackjack.unavailable"));

                if (table.Players.Count >= 5)
                    return (null, "Стол полон (максимум 5 игроков).");

                table.Players.Add(CreatePlayer(discordId, username));
                table.LastActivity = DateTime.UtcNow;

                return (ToDto(gameId, table, discordId), null);
            }
        }
    }

    public (BlackjackTableStateDto state, string error) Hit(ulong gameId, ulong discordId)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return (null, "Стол не найден.");

        lock (table)
        {
            if (!IsOpenTable(gameId, table))
                return (null, "Стол не найден.");

            if (table.Phase != GamePhase.PlayerTurns)
                return (null, "Сейчас нельзя брать карту.");

            var currentPlayer = GetCurrentPlayer(table);
            if (currentPlayer == null || currentPlayer.DiscordId != discordId)
                return (null, "Сейчас не ваш ход.");

            var card = DrawCard(table);
            currentPlayer.Hand.Add(card);
            table.LastActivity = DateTime.UtcNow;

            var total = HandTotal(currentPlayer.Hand);
            if (total > 21)
            {
                currentPlayer.Status = PlayerTurnStatus.Busted;
                AdvanceToNextPlayer(table);
            }
            else if (total == 21)
            {
                currentPlayer.Status = PlayerTurnStatus.Stood;
                AdvanceToNextPlayer(table);
            }

            return (ToDto(gameId, table, discordId), null);
        }
    }

    public (BlackjackTableStateDto state, string error) Stand(ulong gameId, ulong discordId)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return (null, "Стол не найден.");

        lock (table)
        {
            if (!IsOpenTable(gameId, table))
                return (null, "Стол не найден.");

            if (table.Phase != GamePhase.PlayerTurns)
                return (null, "Сейчас нельзя остановиться.");

            var currentPlayer = GetCurrentPlayer(table);
            if (currentPlayer == null || currentPlayer.DiscordId != discordId)
                return (null, "Сейчас не ваш ход.");

            currentPlayer.Status = PlayerTurnStatus.Stood;
            table.LastActivity = DateTime.UtcNow;
            AdvanceToNextPlayer(table);

            return (ToDto(gameId, table, discordId), null);
        }
    }

    public (BlackjackTableStateDto state, string error) StartNewRound(ulong gameId, ulong discordId)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return (null, "Стол не найден.");

        lock (table)
        {
            if (!IsOpenTable(gameId, table))
                return (null, "Стол не найден.");

            var requestingPlayer = table.Players.FirstOrDefault(player =>
                player.DiscordId == discordId);
            if (requestingPlayer == null)
                return (null, "Вы не за этим столом.");
            if (table.RankedGame != null
                && table.RankedGame.CreatorId != requestingPlayer.DiscordId)
                return (null, GameLocalization.MessageForUser(
                    discordId,
                    "kotgh.blackjack.unavailable"));

            if (table.Phase != GamePhase.Waiting && table.Phase != GamePhase.Finished)
                return (null, "Раунд ещё идёт.");

            if (table.Players.Count == 0)
                return (null, "Нет игроков за столом.");

            // Reshuffle if deck is low
            if (table.Deck.Count < 52)
                table.Deck = MakeShoe(6);

            // Reset player states
            foreach (var p in table.Players)
            {
                p.Hand = new List<BlackjackCard>();
                p.Status = PlayerTurnStatus.Playing;
                p.CanSendMessage = false;
            }

            // Deal: 2 cards to each player, then 2 to dealer (second hidden)
            table.DealerHand = new List<BlackjackCard>();

            for (var round = 0; round < 2; round++)
            {
                foreach (var p in table.Players)
                    p.Hand.Add(DrawCard(table));

                var dealerCard = DrawCard(table);
                if (round == 1)
                    dealerCard.FaceUp = false;
                table.DealerHand.Add(dealerCard);
            }

            table.CurrentPlayerIndex = 0;
            table.Phase = GamePhase.PlayerTurns;
            table.LastActivity = DateTime.UtcNow;

            // Skip players with natural 21
            foreach (var p in table.Players)
            {
                if (HandTotal(p.Hand) == 21)
                    p.Status = PlayerTurnStatus.Stood;
            }

            // If first player already stood (natural 21), advance
            if (table.Players[0].Status != PlayerTurnStatus.Playing)
                AdvanceToNextPlayer(table);

            return (ToDto(gameId, table, discordId), null);
        }
    }

    public (BlackjackTableStateDto state, string error) ComposeMessage(ulong gameId, ulong discordId, string[] words)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return (null, "Стол не найден.");

        lock (table)
        {
            if (!IsOpenTable(gameId, table))
                return (null, "Стол не найден.");

            var player = table.Players.FirstOrDefault(p => p.DiscordId == discordId);
            if (player == null)
                return (null, "Вы не за этим столом.");

            if (!player.CanSendMessage)
                return (null, "Вы не выиграли последний раунд.");

            if (words == null || words.Length == 0 || words.Length > 10)
                return (null, "Сообщение должно содержать от 1 до 10 слов.");

            // Validate all words are from the approved list
            var allWords = _wordCategories.SelectMany(c => c.Words).ToHashSet();
            foreach (var w in words)
            {
                if (!allWords.Contains(w))
                    return (null, $"Слово '{w}' не из списка.");
            }

            player.CanSendMessage = false;
            var message = string.Join(" ", words);
            table.LastMessage = new BlackjackMessage
            {
                Author = player.Username,
                Text = message,
                Timestamp = DateTime.UtcNow,
            };
            table.LastActivity = DateTime.UtcNow;

            return (ToDto(gameId, table, discordId), message);
        }
    }

    public bool HasActiveTable(ulong gameId)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return false;

        lock (table)
            return IsOpenTable(gameId, table);
    }

    public bool HasPendingRankedRecovery(ulong discordId)
    {
        foreach (var entry in _tables)
        {
            var table = entry.Value;
            lock (table)
            {
                if (!IsOpenTable(entry.Key, table))
                    continue;

                if (table.Players.Any(player =>
                        player.DiscordId == discordId
                        && player.RankedEloRecoveryEligible
                        && !player.RankedEloRecoverySettled))
                    return true;
            }
        }

        return false;
    }

    public BlackjackTableStateDto GetTableState(ulong gameId, ulong discordId)
    {
        if (!_tables.TryGetValue(gameId, out var table))
            return null;

        lock (table)
        {
            if (!IsOpenTable(gameId, table))
                return null;

            return ToDto(gameId, table, discordId);
        }
    }

    public List<WordCategory> GetWordCategories()
    {
        return _wordCategories;
    }

    // ── Internal game logic ────────────────────────────────────────────

    private GameClass FindActiveGame(ulong gameId)
    {
        lock (_global.GamesList)
            return _global.GamesList.Find(game => game.GameId == gameId);
    }

    private bool IsOpenTable(ulong gameId, BlackjackTable table)
    {
        return !table.Closed
               && _tables.TryGetValue(gameId, out var current)
               && ReferenceEquals(current, table);
    }

    private static BlackjackPlayer CreatePlayer(ulong discordId, string username) => new()
    {
        DiscordId = discordId,
        Username = username,
        Hand = new List<BlackjackCard>(),
        Status = PlayerTurnStatus.Waiting,
        Wins = 0,
        CanSendMessage = false,
    };

    private static List<BlackjackCard> MakeShoe(int deckCount)
    {
        var suits = new[] { "spades", "clubs", "hearts", "diamonds" };
        var ranks = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        var shoe = new List<BlackjackCard>();

        for (var d = 0; d < deckCount; d++)
            foreach (var suit in suits)
                foreach (var rank in ranks)
                    shoe.Add(new BlackjackCard { Suit = suit, Rank = rank, FaceUp = true });

        // Fisher-Yates shuffle
        var rng = new Random();
        for (var i = shoe.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shoe[i], shoe[j]) = (shoe[j], shoe[i]);
        }

        return shoe;
    }

    private static BlackjackCard DrawCard(BlackjackTable table)
    {
        if (table.Deck.Count == 0)
            table.Deck = MakeShoe(6);

        var card = table.Deck[^1];
        table.Deck.RemoveAt(table.Deck.Count - 1);
        return card;
    }

    private static int CardValue(BlackjackCard card)
    {
        if (card.Rank is "J" or "Q" or "K") return 10;
        if (card.Rank == "A") return 11;
        return int.Parse(card.Rank);
    }

    private static int HandTotal(List<BlackjackCard> hand)
    {
        var total = 0;
        var aces = 0;
        foreach (var c in hand)
        {
            total += CardValue(c);
            if (c.Rank == "A") aces++;
        }
        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }
        return total;
    }

    private static BlackjackPlayer GetCurrentPlayer(BlackjackTable table)
    {
        if (table.CurrentPlayerIndex < 0 || table.CurrentPlayerIndex >= table.Players.Count)
            return null;
        return table.Players[table.CurrentPlayerIndex];
    }

    private void AdvanceToNextPlayer(BlackjackTable table)
    {
        // Find next player who is still Playing
        for (var i = table.CurrentPlayerIndex + 1; i < table.Players.Count; i++)
        {
            if (table.Players[i].Status == PlayerTurnStatus.Playing)
            {
                table.CurrentPlayerIndex = i;
                return;
            }
        }

        // All players done — dealer turn
        DealerTurn(table);
    }

    private void DealerTurn(BlackjackTable table)
    {
        table.Phase = GamePhase.DealerTurn;

        // Reveal hidden card
        foreach (var c in table.DealerHand)
            c.FaceUp = true;

        // Check if any player is not busted (dealer needs to draw)
        var anyActive = table.Players.Any(p => p.Status == PlayerTurnStatus.Stood);
        if (anyActive)
        {
            // Dealer hits until 17+
            while (HandTotal(table.DealerHand) < 17)
                table.DealerHand.Add(DrawCard(table));
        }

        // Resolve
        ResolveRound(table);
    }

    private void ResolveRound(BlackjackTable table)
    {
        var dealerTotal = HandTotal(table.DealerHand);
        var dealerBusted = dealerTotal > 21;
        var dealerBlackjack = dealerTotal == 21 && table.DealerHand.Count == 2;

        foreach (var p in table.Players)
        {
            var pTotal = HandTotal(p.Hand);
            var playerBlackjack = pTotal == 21 && p.Hand.Count == 2;

            if (p.Status == PlayerTurnStatus.Busted)
            {
                p.Result = "loss";
            }
            else if (playerBlackjack && !dealerBlackjack)
            {
                p.Result = "blackjack";
                p.Wins++;
                p.CanSendMessage = true;
            }
            else if (dealerBlackjack && !playerBlackjack)
            {
                p.Result = "loss";
            }
            else if (dealerBusted)
            {
                p.Result = "win";
                p.Wins++;
                p.CanSendMessage = true;
            }
            else if (pTotal > dealerTotal)
            {
                p.Result = "win";
                p.Wins++;
                p.CanSendMessage = true;
            }
            else if (dealerTotal > pTotal)
            {
                p.Result = "loss";
            }
            else
            {
                p.Result = "push";
            }

            SettleRankedEloRecovery(table, p);
        }

        table.Phase = GamePhase.Finished;
    }

    private void SettleRankedEloRecovery(BlackjackTable table, BlackjackPlayer player)
    {
        if (!player.RankedEloRecoveryEligible || player.RankedEloRecoverySettled)
            return;

        player.RankedEloRecoverySettled = true;
        if (player.Result is not "win" and not "blackjack")
            return;

        var account = _userAccounts.GetAccount(player.DiscordId);
        if (account == null)
            return;

        lock (account)
        {
            account.EloRating += RankedElo.BlackjackRecovery;
            if (!_userAccounts.SaveAccount(account))
                Console.WriteLine(
                    $"[Blackjack] Ranked recovery for {player.DiscordId} is retained in memory for a save retry.");

            player.RankedEloRecoveryAwarded = RankedElo.BlackjackRecovery;
            var game = table.RankedGame;
            if (game != null)
            {
                game.RankedBlackjackRecoveryAwarded = RankedElo.BlackjackRecovery;
                if (game.RankedEloSettlement != null)
                {
                    var previous = game.RankedEloSettlement;
                    game.RankedEloSettlement = new GameClass.RankedEloSettlementClass
                    {
                        RatingBefore = previous.RatingBefore,
                        RatingAfter = account.EloRating,
                        Delta = account.EloRating - previous.RatingBefore,
                        FinalPlace = previous.FinalPlace,
                        PlacementDelta = previous.PlacementDelta,
                        ShinigamiPenalty = previous.ShinigamiPenalty,
                        BlackjackRecovery = RankedElo.BlackjackRecovery,
                    };
                    _global.StoreFinishedGame(game);
                }
            }
        }
    }

    private void CleanupStaleTables()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        foreach (var kvp in _tables)
        {
            var table = kvp.Value;
            lock (table)
            {
                if (!IsOpenTable(kvp.Key, table) || table.LastActivity >= cutoff)
                    continue;

                if (_tables.TryRemove(kvp.Key, out var removed)
                    && ReferenceEquals(removed, table))
                {
                    table.Closed = true;
                    Console.WriteLine($"[Blackjack] Cleaned up stale table for game {kvp.Key}");
                }
            }
        }
    }

    // ── DTO mapping ────────────────────────────────────────────────────

    private BlackjackTableStateDto ToDto(
        ulong gameId,
        BlackjackTable table,
        ulong requestingDiscordId)
    {
        var dealerCards = table.DealerHand?.Select(c => new BlackjackCardDto
        {
            Suit = c.FaceUp ? c.Suit : null,
            Rank = c.FaceUp ? c.Rank : null,
            FaceUp = c.FaceUp,
        }).ToList() ?? new List<BlackjackCardDto>();

        var dealerTotal = table.DealerHand != null
            ? HandTotal(table.DealerHand.Where(c => c.FaceUp).ToList())
            : 0;

        // Show true total when dealer's turn or finished
        if (table.Phase is GamePhase.DealerTurn or GamePhase.Finished && table.DealerHand != null)
            dealerTotal = HandTotal(table.DealerHand);

        return new BlackjackTableStateDto
        {
            GameId = gameId,
            Phase = table.Phase.ToString().ToLowerInvariant(),
            CurrentPlayerIndex = table.CurrentPlayerIndex,
            DealerName = "Рюк",
            DealerHand = dealerCards,
            DealerTotal = dealerTotal,
            LastMessage = table.LastMessage != null ? new BlackjackMessageDto
            {
                Author = table.LastMessage.Author,
                Text = table.LastMessage.Text,
            } : null,
            WordCategories = _wordCategories.Select(c => new WordCategoryDto
            {
                Name = c.Name,
                Words = c.Words,
            }).ToList(),
            RankedEloRecovery = BuildRankedEloRecovery(table, requestingDiscordId),
            Players = table.Players.Select((p, idx) => new BlackjackPlayerDto
            {
                DiscordId = p.DiscordId.ToString(),
                Username = p.Username,
                Hand = p.Hand.Select(c => new BlackjackCardDto
                {
                    Suit = c.Suit,
                    Rank = c.Rank,
                    FaceUp = c.FaceUp,
                }).ToList(),
                Total = HandTotal(p.Hand),
                Status = p.Status.ToString().ToLowerInvariant(),
                Result = p.Result,
                Wins = p.Wins,
                IsCurrentTurn = table.Phase == GamePhase.PlayerTurns && idx == table.CurrentPlayerIndex,
                IsMe = p.DiscordId == requestingDiscordId,
                CanSendMessage = p.DiscordId == requestingDiscordId && p.CanSendMessage,
            }).ToList(),
        };
    }

    private RankedEloRecoveryDto BuildRankedEloRecovery(BlackjackTable table, ulong requestingDiscordId)
    {
        var player = table.Players.FirstOrDefault(candidate =>
            candidate.DiscordId == requestingDiscordId && candidate.RankedEloRecoveryEligible);
        if (player == null)
            return null;

        var account = _userAccounts.GetAccount(requestingDiscordId);
        return new RankedEloRecoveryDto
        {
            RatingBeforePenalty = player.RankedEloRatingBeforePenalty,
            CurrentRating = account?.EloRating
                            ?? player.RankedEloRatingBeforePenalty + RankedElo.ShinigamiPenalty
                            + player.RankedEloRecoveryAwarded,
            Penalty = RankedElo.ShinigamiPenalty,
            Recovered = player.RankedEloRecoveryAwarded,
            Remaining = player.RankedEloRecoverySettled
                ? 0
                : RankedElo.BlackjackRecovery,
            Settled = player.RankedEloRecoverySettled,
        };
    }

    // ── Internal models ────────────────────────────────────────────────

    private class BlackjackTable
    {
        public List<BlackjackPlayer> Players { get; set; } = new();
        public List<BlackjackCard> Deck { get; set; } = MakeShoe(6);
        public List<BlackjackCard> DealerHand { get; set; } = new();
        public GamePhase Phase { get; set; } = GamePhase.Waiting;
        public int CurrentPlayerIndex { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public BlackjackMessage LastMessage { get; set; }
        public GameClass RankedGame { get; set; }
        public bool Closed { get; set; }
    }

    private class BlackjackPlayer
    {
        public ulong DiscordId { get; set; }
        public string Username { get; set; }
        public List<BlackjackCard> Hand { get; set; } = new();
        public PlayerTurnStatus Status { get; set; }
        public string Result { get; set; }
        public int Wins { get; set; }
        public bool CanSendMessage { get; set; }
        public bool RankedEloRecoveryEligible { get; set; }
        public bool RankedEloRecoverySettled { get; set; }
        public int RankedEloRatingBeforePenalty { get; set; }
        public int RankedEloRecoveryAwarded { get; set; }
    }

    private class BlackjackCard
    {
        public string Suit { get; set; }
        public string Rank { get; set; }
        public bool FaceUp { get; set; } = true;
    }

    private class BlackjackMessage
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private enum GamePhase { Waiting, PlayerTurns, DealerTurn, Finished }
    private enum PlayerTurnStatus { Waiting, Playing, Stood, Busted }

    // ── JSON deserialization model ─────────────────────────────────────

    private class WordListFile
    {
        public List<WordCategory> Categories { get; set; }
    }
}

public class WordCategory
{
    public string Name { get; set; }
    public List<string> Words { get; set; }
}
