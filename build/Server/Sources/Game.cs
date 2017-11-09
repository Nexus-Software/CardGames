﻿using CardGameResources.Game;
using CardGameResources.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Servers.Sources
{
    class Game
    {
        public Game()
        {
        }

        /**
         * 
         * ENTRY/EXIT POINTS
         * 
         */

        public void StartGame()
        {
            this.Init();
            this.Run();
            this.End();
        }

        private void End()
        {
            Console.WriteLine("End");
        }

        /**
         * 
         * TOOLS
         * 
         */

        private void FillUserDeck()
        {
            Random random = new Random();
            foreach (string username in this.Users)
            {
                while (this.UsersDeck[username].Array.Count() < 8 && masterCopy.Array.Count() > 0)
                {
                    int index = random.Next(0, masterCopy.Array.Count());
                    Card card = masterCopy.Array.ElementAt(index);
                    this.UsersDeck[username].Add(card);
                    Console.WriteLine("Adding new card in the " + username + "'s deck: " + card.Value + ":" + card.Color + ". (" + masterCopy.Array.Count() + " left in the deck)");
                    masterCopy.Remove(index);
                }
                this.Send(username, PacketType.GAME, new Gamecall(GameAction.S_SET_USER_DECK, this.UsersDeck[username]));
            }
        }

        private void TrumpDecision()
        {
            Console.WriteLine("About to choose what'll be the potential trump...");
            Random random = new Random();
            int index = random.Next(0, this.masterCopy.Array.Count());
            this.TrumpInfos = new TrumpInfos(this.masterCopy.Array.ElementAt(index));
            this.masterCopy.Remove(index);
            Console.WriteLine("The trump is set to " + this.TrumpInfos.Card.Value + ":" + this.TrumpInfos.Card.Color);
            this.Send(PacketType.GAME, new Gamecall(GameAction.S_SET_BOARD_DECK, new Deck(new List<Card> { this.TrumpInfos.Card })));
        }

        private void GiveCards()
        {
            Console.WriteLine("About to give some cards...");
            this.InitMasterDeck();
            this.InitUsersDeck();
        }

        private int GetCardValue(Card card)
        {
            if (card == null)
                return 0;
            return (card.Color == this.TrumpInfos.RealColor) ? TrumpCardValues[card.Value] : BasicCardValues[card.Value];
        }

        private int GetCardPoints(Card card)
        {
            if (card == null)
                return 0;
            return (card.Color == this.TrumpInfos.RealColor) ? TrumpCardPoints[card.Value] : BasicCardPoints[card.Value];
        }

        private string CheckDeckWinner()
        {
            string mainColor = this.LastRound[this.CurrentPlayerName].Color;
            string tcolor = this.TrumpInfos.RealColor;
            KeyValuePair<string, Card> maxItem = new KeyValuePair<string, Card>("", this.TrumpInfos.Card);
            int score = 0;
            foreach (var item in this.LastRound)
            {
                if (maxItem.Key == "" || (
                    (item.Value.Color == maxItem.Value.Color && this.GetCardValue(item.Value) > this.GetCardValue(maxItem.Value))
                    || (item.Value.Color == tcolor && maxItem.Value.Color != tcolor)
                    || (item.Value.Color == tcolor && maxItem.Value.Color == tcolor && this.GetCardValue(item.Value) > this.GetCardValue(maxItem.Value))))
                {
                    maxItem = item;
                }
                score += this.GetCardPoints(item.Value);
            }
            int team = this.Teams[maxItem.Key];
            this.Scores[team] += score;
            this.Send(PacketType.ENV, new Envcall(EnvInfos.S_SCORES, this.Scores));
            Console.WriteLine(maxItem.Key + " won this lap with " + score + " points");
            Console.WriteLine("Team 1 : " + this.Scores.ElementAt(0) + " - " + this.Scores.ElementAt(1) + " : Team 2");
            return maxItem.Key;
        }

        private List<string> GetUserListFrom(string begin)
        {
            Console.WriteLine("Generating new user list where root is" + begin);
            List<string> list = new List<string>();
            List<string> tpUser = this.Users;
            tpUser.Reverse();
            int beginIndex = tpUser.FindIndex(begin.StartsWith);
            for (int i = 0; i < 4; ++i)
            {
                list.Add(tpUser.ElementAt((beginIndex + i) % 4));
                Console.WriteLine("-> " + list.ElementAt(i));
            }
            return list;
        }

        /**
         * 
         * GAME INIT
         * 
         */

        private void Init()
        {
            Console.WriteLine("Starting game...");
            this.Send(PacketType.SYS, new Syscall(SysCommand.S_START_GAME, null));

            Thread.Sleep(500);

            Console.WriteLine("Setting teams randomly...");
            int x = 0;
            foreach (var username in this.Users)
            {
                this.Teams.Add(username, x % 2);
                Console.WriteLine("Team " + this.Teams[username].ToString() + " has been assigned to " + username + ".");
                x++;
            }
            this.Send(PacketType.ENV, new Envcall(EnvInfos.S_SET_TEAM, this.Teams));
            this.GiveCards();
            this.TrumpDecision();
            this.Send(PacketType.ENV, new Envcall(EnvInfos.S_SCORES, this.Scores));
        }

        private void InitMasterDeck()
        {
            foreach (string color in this.Colors)
            {
                foreach (char c in "789tjqka")
                {
                    this.masterDeck.Add(new Card(c, color));
                    Console.WriteLine("New Card: /" + color + "/" + c + ".png");
                }
            }
            this.masterCopy = this.masterDeck;
        }

        private void InitUsersDeck()
        {
            Random random = new Random();
            foreach (string username in this.Users)
            {
                this.UsersDeck.Add(username, new Deck());
                for (int x = 0; x < 5; ++x)
                {
                    int index = random.Next(0, masterCopy.Array.Count());
                    Card card = masterCopy.Array.ElementAt(index);
                    this.UsersDeck[username].Add(card);
                    Console.WriteLine("Adding new card in the " + username + "'s deck: " + card.Value + ":" + card.Color + ". (" + masterCopy.Array.Count() + " left in the deck)");
                    masterCopy.Remove(index);
                }
                this.Send(username, PacketType.GAME, new Gamecall(GameAction.S_SET_USER_DECK, this.UsersDeck[username]));
            }
        }

        /**
         * 
         * GAME CORE
         * 
         */

        private void Run()
        {
            this.TrumpPhase();
            this.PlayPhase();
            Console.WriteLine("Run");
        }

            /**
             * 
             * TRUMP PHASE
             * 
             */

        private bool TrumpPhase()
        {
            this.TrumpPhase_lock = true;
            int phase = 1;
            Console.WriteLine("Trump Phase");
            while (TrumpPhase_lock1 && phase <= 2)
            {
                Console.WriteLine("Turn " + phase);
                foreach (var user in this.Users)
                {
                    Console.WriteLine("New turn for " + user);
                    this.TrumpPhaseInitLock(phase);
                    this.CurrentPlayerName = user;
                    this.Send(PacketType.ENV, new Envcall(EnvInfos.S_SET_TOUR, user));
                    this.Send(user, PacketType.GAME, new Gamecall(GameAction.S_REQUEST_TRUMP_FROM, new KeyValuePair<int, string>(phase, user)));
                    this.TrumpPhaseWait(phase);
                    if (this.TrumpInfos.Owner != null)
                        break;
                }
                phase++;
            }
            Console.WriteLine("Exiting Trump phase at turn " + phase);
            if (this.TrumpPhase_lock)
            {
                Console.WriteLine("Abort Game");
                return false;
            }
            Console.WriteLine("Launch Game!");
            this.Send(PacketType.GAME, new Gamecall(GameAction.S_SET_TRUMP, this.TrumpInfos));
            return true;
        }

        private void TrumpPhaseInitLock(int phase)
        {
            if (phase == 1)
                this.TakeTrump_lock = true;
            else
                this.TakeTrumpAs_lock = true;
        }

        private void TrumpPhaseWait(int phase)
        {
            if (phase == 1)
                while (this.TakeTrump_lock) ;
            else
                while (this.TakeTrumpAs_lock) ;
        }

        private void AssignTrump(string name, string color)
        {
            this.TrumpInfos.Owner = name;
            this.TrumpInfos.RealColor = color;
            this.UsersDeck[name].Add(this.TrumpInfos.Card);
        }

        public void TakeTrump_callback(string name, bool ans)
        {
            Console.WriteLine("Got Take trump: " + ans + " for " + name);

            if (name != this.CurrentPlayerName)
                return;

            if (ans)
            {
                this.AssignTrump(name, this.TrumpInfos.Card.Color);
                this.TrumpPhase_lock = false;
                Console.WriteLine("Trump updated, exiting phase...");
            }

            this.TakeTrump_lock = false;
        }

        public void TakeTrumpAs_callback(string name, string color)
        {
            Console.WriteLine("Got Take trump as : " + color + " for " + name);

            if (name != this.CurrentPlayerName)
                return;

            if (color != null && this.Colors.Contains(color))
            {
                this.AssignTrump(name, color);
                this.TrumpPhase_lock = false;
                Console.WriteLine("Trump updated, exiting phase...");
            }

            this.TakeTrumpAs_lock = false;
        }

            /**
             * 
             * PLAY PHASE
             * 
             */

        private bool PlayPhase()
        {
            this.FillUserDeck();
            Console.WriteLine("Game Phase");
            this.PlayPhase_lock = true;
            this.CurrentPlayerName = this.Users.ElementAt(0);
            while (this.PlayPhase_lock)
            {
                this.LastRound.Clear();
                this.BoardDeck.Clear();
                this.Send(PacketType.GAME, new Gamecall(GameAction.S_SET_BOARD_DECK, this.BoardDeck));
                List<string> roundUserList = this.GetUserListFrom(this.CurrentPlayerName);
                foreach (var user in roundUserList)
                {
                    Console.WriteLine("New turn for " + user);
                    this.GamePlayTurn_lock = true;
                    this.CurrentPlayerName = user;
                    this.Send(PacketType.ENV, new Envcall(EnvInfos.S_SET_TOUR, user));
                    while (this.GamePlayTurn_lock) ;
                }
                this.CurrentPlayerName = this.CheckDeckWinner();
                this.Send(PacketType.GAME, new Gamecall(GameAction.S_SET_LASTROUND_DECK, this.LastRound));
            }
            return true;
        }


        private int GamePlayCheckMove(Deck userDeck, Card playedCard, Deck board, string color, bool powerCheck)
        {
            Deck colorDeck = new Deck();
            foreach (var c in userDeck.Array)
            {
                if (c.Color == color)
                    colorDeck.Add(c);
            }
            if (colorDeck.Array.Count() == 0)
                return 4;

            if (playedCard.Color != color)
                return 3;

            if (powerCheck)
            {
                Card maxColorCard = (board.Array.Count() > 0)? board.Array.ElementAt(0): null;
                foreach (var c in BoardDeck.Array)
                {

                    if (this.GetCardValue(maxColorCard) < this.GetCardValue(c) && c.Color == color)
                    {
                        maxColorCard = c;
                    }
                }
                bool haveGreater = false;
                foreach (var c in colorDeck.Array)
                {
                    if (maxColorCard == null || this.GetCardValue(c) > this.GetCardValue(maxColorCard))
                        haveGreater = true;
                }
                if (haveGreater && this.GetCardValue(playedCard) < this.GetCardValue(maxColorCard))
                    return 1;
            }
            return 0;
        }

        private void PlayCard(string name, Card card)
        {
            this.UsersDeck[name].Remove(card);
            this.BoardDeck.Add(card);
            this.LastRound.Add(name, card);
            this.Send(PacketType.GAME, new Gamecall(GameAction.S_SET_BOARD_DECK, this.BoardDeck));
            this.Send(name, PacketType.GAME, new Gamecall(GameAction.S_SET_USER_DECK, this.UsersDeck[name]));
            this.GamePlayTurn_lock = false;
        }

        public void PlayCard_callback(string name, Card card)
        {
            if (name != this.CurrentPlayerName)
                return;

            if (this.UsersDeck[name].Contains(card))
            {
                //Get main color if it exists
                string color = card.Color;
                if (this.BoardDeck.Array.Count() > 0)
                    color = BoardDeck.Array.ElementAt(0).Color;
                int colorCheck = (color != this.TrumpInfos.RealColor)? this.GamePlayCheckMove(this.UsersDeck[name], card, BoardDeck, color, false): 4;
                switch (colorCheck)
                {
                    case 0:
                        this.PlayCard(name, card);
                        break;
                    case 3:
                        this.Send(name, PacketType.ERR, new Errcall(Err.FORBIDDEN_CARD, "You have some " + color + " to play!"));
                        break;
                    case 4:
                        int trumpCheck = this.GamePlayCheckMove(this.UsersDeck[name], card, BoardDeck, this.TrumpInfos.RealColor, true);
                        switch (trumpCheck)
                        {
                            case 0:
                                this.PlayCard(name, card);
                                break;
                            case 1:
                                this.Send(name, PacketType.ERR, new Errcall(Err.FORBIDDEN_CARD, "You have a better card than a " + card.Value + " of " + card.Color + "(trump) to play!"));
                                break;
                            case 3:
                                this.Send(name, PacketType.ERR, new Errcall(Err.FORBIDDEN_CARD, "You have some " + TrumpInfos.RealColor + "(which is trump color) to play!"));
                                break;
                            case 4:
                                this.PlayCard(name, card);
                                break;
                        }
                        break;
                }
            }
       }

        /**
         * 
         * NETWORK COMMUNICATION
         * 
         */

        private void ReplacePlayer(string name)
        {
            Console.WriteLine("Connection lost for " + name);
        }

        public bool Send(string name, PacketType type, Object data)
        {
            try
            {
                Network.Server.Instance.sendDataToClient(name, new Packet("root", type, data));
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                this.ReplacePlayer(name);
            }
            return true;
        }

        public bool Send(PacketType type, Object data)
        {
            try
            {
                foreach (var username in this.Users)
                {
                    Network.Server.Instance.sendDataToClient(username, new Packet("root", type, data));
                }
                
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);

            }
            return true;
        }

        /**
         *
         * VARIABLES
         * 
         */

        // Trump infos
        private TrumpInfos trumpInfos = null;
        // Useful decks and cards containers
        private Deck masterDeck = new Deck();
        private Deck masterCopy = new Deck();
        private Deck boardDeck = new Deck();
        private Dictionary<string, Deck> usersDeck = new Dictionary<string, Deck> { };
        private Dictionary<string, Card> lastRound = new Dictionary<string, Card>();
        private string currentPlayerName = "";
        // Utils definitions
        private Dictionary<string, int> teams = new Dictionary<string, int> { };
        private List<string> users = new List<string> { };
        private List<String> colors = new List<string> { "clubs", "diamond", "hearts", "spades" };
        // Points counting tools
        private List<int> scores = new List<int> { 0, 0 };
        private List<Deck> points = new List<Deck> { new Deck(), new Deck() };
        // Cards values
        private Dictionary<char, int> basicCardValues = new Dictionary<char, int> { { '7', 1 }, { '8', 2 }, { '9', 3 }, { 'j', 4 }, { 'q', 5 }, { 'k', 6 }, { 't', 7 }, { 'a', 8 } };
        private Dictionary<char, int> trumpCardValues = new Dictionary<char, int> { { '7', 1 }, { '8', 2 }, { 'q', 3 }, { 'k', 4 }, { 't', 5 }, { 'a', 6 }, { '9', 7 }, { 'j', 8 } };
        // Cards Points
        private Dictionary<char, int> basicCardPoints = new Dictionary<char, int> { { '7', 0 }, { '8', 0 }, { '9', 0 }, { 'j', 2 }, { 'q', 3 }, { 'k', 4 }, { 't', 10 }, { 'a', 11 } };
        private Dictionary<char, int> trumpCardPoints = new Dictionary<char, int> { { '7', 0 }, { '8', 0 }, { 'q', 3 }, { 'k', 4 }, { 't', 10 }, { 'a', 11 }, { '9', 14 }, { 'j', 20 } };
        // States
        private bool takeTrump_lock = false;
        private bool takeTrumpAs_lock = false;
        private bool gamePlayTurn_lock = false;
        private bool trumpPhase_lock = false;
        private bool playPhase_lock = false;

        public List<string> Users { get => users; set => users = value; }
        public Dictionary<string, int> Teams { get => teams; set => teams = value; }
        public Dictionary<string, Deck> UsersDeck { get => usersDeck; set => usersDeck = value; }
        public TrumpInfos TrumpInfos { get => trumpInfos; set => trumpInfos = value; }
        public string CurrentPlayerName { get => currentPlayerName; set => currentPlayerName = value; }
        public Dictionary<string, Card> LastRound { get => lastRound; set => lastRound = value; }
        public bool TrumpPhase_lock { get => TrumpPhase_lock1; set => TrumpPhase_lock1 = value; }
        public List<string> Colors { get => colors; set => colors = value; }
        public List<int> Scores { get => scores; set => scores = value; }
        public List<Deck> Points { get => points; set => points = value; }
        public Deck BoardDeck { get => boardDeck; set => boardDeck = value; }
        public bool TakeTrump_lock { get => takeTrump_lock; set => takeTrump_lock = value; }
        public bool TakeTrumpAs_lock { get => takeTrumpAs_lock; set => takeTrumpAs_lock = value; }
        public bool GamePlayTurn_lock { get => gamePlayTurn_lock; set => gamePlayTurn_lock = value; }
        public bool TrumpPhase_lock1 { get => trumpPhase_lock; set => trumpPhase_lock = value; }
        public bool PlayPhase_lock { get => playPhase_lock; set => playPhase_lock = value; }
        public Dictionary<char, int> TrumpCardValues { get => trumpCardValues; set => trumpCardValues = value; }
        public Dictionary<char, int> BasicCardValues { get => basicCardValues; set => basicCardValues = value; }
        public Dictionary<char, int> BasicCardPoints { get => basicCardPoints; set => basicCardPoints = value; }
        public Dictionary<char, int> TrumpCardPoints { get => trumpCardPoints; set => trumpCardPoints = value; }
    }
}
