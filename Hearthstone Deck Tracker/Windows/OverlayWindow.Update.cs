﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.LogReader.Handlers;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.BoardDamage;
using Hearthstone_Deck_Tracker.Utility.Logging;
using static System.Windows.Visibility;
using static HearthDb.Enums.GameTag;
using static Hearthstone_Deck_Tracker.Controls.Overlay.WotogCounterStyle;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Controls.Overlay.Mercenaries;

namespace Hearthstone_Deck_Tracker.Windows
{
	public partial class OverlayWindow
	{
		private async void SetTopmost()
		{
			if(User32.GetHearthstoneWindow() == IntPtr.Zero)
			{
				Log.Info("Hearthstone window not found");
				return;
			}

			for(var i = 0; i < 20; i++)
			{
				var isTopmost = User32.IsTopmost(new WindowInteropHelper(this).Handle);
				if(isTopmost)
				{
					Log.Info($"Overlay is topmost after {i + 1} tries.");
					return;
				}

				Topmost = false;
				Topmost = true;
				await Task.Delay(250);
			}

			Log.Info("Could not set overlay as topmost");
		}

		public void Update(bool refresh)
		{
			if (refresh)
			{
				ListViewPlayer.Items.Refresh();
				ListViewOpponent.Items.Refresh();
				SetTopmost();
			}

			var opponentHandCount = _game.Opponent.HandCount;
			for (var i = 0; i < 10; i++)
			{
				if (i < opponentHandCount)
				{
					var entity = _game.Opponent.Hand.FirstOrDefault(x => x.GetTag(ZONE_POSITION) == i + 1);
					if(entity == null)
						continue;
					if(!Config.Instance.HideOpponentCardAge)
						_cardMarks[i].UpdateCardAge(entity.Info.Turn);
					else 
						_cardMarks[i].UpdateCardAge(null);
					if(!Config.Instance.HideOpponentCardMarks)
					{
						_cardMarks[i].UpdateIcon(entity.Info.CardMark);
						if(entity.Info.CardMark == CardMark.Created || entity.Info.CardMark == CardMark.DrawnByEntity)
						{
							var creatorId = entity.Info.GetCreatorId();
							if(creatorId > 0 && _game.Entities.TryGetValue(creatorId, out var creator))
								_cardMarks[i].UpdateSourceCard(creator.Card);
							else
								_cardMarks[i].UpdateSourceCard(null);
						}
						else if(entity.Info.GetDrawerId() != null)
						{
							var drawerId = entity.Info.GetDrawerId();
							if(drawerId > 0 && _game.Entities.TryGetValue(drawerId ?? 0, out var drawer))
								_cardMarks[i].UpdateSourceCard(drawer.Card);
							else
								_cardMarks[i].UpdateSourceCard(null);
						}
						else
							_cardMarks[i].UpdateSourceCard(null);
						_cardMarks[i].UpdateCostReduction(entity.Info.CostReduction);
					}
					else
					{
						_cardMarks[i].UpdateIcon(CardMark.None);
						_cardMarks[i].UpdateSourceCard(null);
					}
					_cardMarks[i].Visibility = _game.IsInMenu || Config.Instance.HideOpponentCardAge && Config.Instance.HideOpponentCardMarks
												   ? Hidden : Visible;
				}
				else
					_cardMarks[i].Visibility = Collapsed;
			}

			var foo = Core.Game.Entities;
			var oppBoard = Core.Game.Opponent.Board.Where(x => x.IsMinion).OrderBy(x => x.GetTag(ZONE_POSITION)).ToList();
			var playerBoard = Core.Game.Player.Board.Where(x => x.IsMinion).OrderBy(x => x.GetTag(ZONE_POSITION)).ToList();
			UpdateMouseOverDetectionRegions(oppBoard, playerBoard);
			if(!_game.IsInMenu && (_game.IsMulliganDone || _game.IsBattlegroundsMatch || _game.IsMercenariesMatch) && User32.IsHearthstoneInForeground() && IsVisible)
				DetectMouseOver(playerBoard, oppBoard);
			else
				FlavorTextVisibility = Collapsed;

			UpdateInteractiveElements();

			StackPanelPlayer.Opacity = Config.Instance.PlayerOpacity / 100;
			StackPanelOpponent.Opacity = Config.Instance.OpponentOpacity / 100;
			StackPanelSecrets.Opacity = Config.Instance.SecretsOpacity / 100;
			Opacity = Config.Instance.OverlayOpacity / 100;

			var inBattlegrounds = _game.IsBattlegroundsMatch;
			var inMercenaries = _game.IsMercenariesMatch;
			var hideDeck = Config.Instance.HideDecksInOverlay || inBattlegrounds || inMercenaries || (Config.Instance.HideInMenu && _game.IsInMenu);

			if (!_playerCardsHidden)
			{
				StackPanelPlayer.Visibility =  hideDeck && !_uiMovable ? Collapsed : Visible;
			}

			if (!_opponentCardsHidden)
			{
				StackPanelOpponent.Visibility = hideDeck && !_uiMovable ? Collapsed : Visible;
			}

			CanvasPlayerChance.Visibility = Config.Instance.HideDrawChances ? Collapsed : Visible;
			LblPlayerFatigue.Visibility = Config.Instance.HidePlayerFatigueCount ? Collapsed : Visible;
			CanvasPlayerCount.Visibility = Config.Instance.HidePlayerCardCount ? Collapsed : Visible;

			CanvasOpponentChance.Visibility = Config.Instance.HideOpponentDrawChances ? Collapsed : Visible;
			LblOpponentFatigue.Visibility = Config.Instance.HideOpponentFatigueCount ? Collapsed : Visible;
			CanvasOpponentCount.Visibility = Config.Instance.HideOpponentCardCount ? Collapsed : Visible;

			if (_game.IsInMenu && !_uiMovable)
				HideTimers();

			ListViewOpponent.Visibility = Config.Instance.HideOpponentCards ? Collapsed : Visible;
			ListViewPlayer.Visibility = Config.Instance.HidePlayerCards ? Collapsed : Visible;

			var gameStarted = !_game.IsInMenu && _game.SetupDone && _game.Player.PlayerEntities.Any();
			SetCardCount(_game.Player.HandCount, !gameStarted ? 30 : _game.Player.DeckCount);

			SetOpponentCardCount(_game.Opponent.HandCount, !gameStarted ? 30 : _game.Opponent.DeckCount);


			LblWins.Visibility = Config.Instance.ShowDeckWins && _game.IsUsingPremade ? Visible : Collapsed;
			LblDeckTitle.Visibility = Config.Instance.ShowDeckTitle && _game.IsUsingPremade ? Visible : Collapsed;
			LblWinRateAgainst.Visibility = Config.Instance.ShowWinRateAgainst && _game.IsUsingPremade
											   ? Visible : Collapsed;

			var showWarning = !_game.IsInMenu && DeckList.Instance.ActiveDeckVersion != null && DeckManager.NotFoundCards.Any() && DeckManager.IgnoredDeckId != DeckList.Instance.ActiveDeckVersion.DeckId;
			StackPanelWarning.Visibility = showWarning ? Visible : Collapsed;
			if(showWarning)
			{
				LblWarningCards.Text = string.Join(", ", DeckManager.NotFoundCards.Take(Math.Min(DeckManager.NotFoundCards.Count, 3)).Select(c => c.LocalizedName));
				if(DeckManager.NotFoundCards.Count > 3)
					LblWarningCards.Text += ", ...";
			}

			if(_game.IsInMenu || !inBattlegrounds)
			{
				HideBgsTopBar();
			}

			UpdateIcons();

			SetDeckTitle();
			SetWinRates();

			UpdateElementSizes();
			UpdateElementPositions();

			if (Core.Windows.PlayerWindow.Visibility == Visible)
				Core.Windows.PlayerWindow.Update();
			if (Core.Windows.OpponentWindow.Visibility == Visible)
				Core.Windows.OpponentWindow.Update();
		}

		private void UpdateIcons()
		{
			var inBattlegrounds = _game.IsBattlegroundsMatch;
			var inMercenaries = _game.IsMercenariesMatch;

			IconBoardAttackPlayer.Visibility = Config.Instance.HidePlayerAttackIcon || _game.IsInMenu || inBattlegrounds || inMercenaries ? Collapsed : Visible;
			IconBoardAttackOpponent.Visibility = Config.Instance.HideOpponentAttackIcon || _game.IsInMenu || inBattlegrounds || inMercenaries ? Collapsed : Visible;

			// do the calculation if at least one of the icons is visible
			if (_game.SetupDone && (IconBoardAttackPlayer.Visibility == Visible || IconBoardAttackOpponent.Visibility == Visible))
			{
				var board = new BoardState();
				TextBlockPlayerAttack.Text = board.Player.Damage.ToString();
				TextBlockOpponentAttack.Text = board.Opponent.Damage.ToString();
			}


			var showPlayerCthunCounter = WotogCounterHelper.ShowPlayerCthunCounter;
			var showPlayerSpellsCounter = WotogCounterHelper.ShowPlayerSpellsCounter;
			var showPlayerJadeCounter = WotogCounterHelper.ShowPlayerJadeCounter;
			var showPlayerPogoHopperCounter = WotogCounterHelper.ShowPlayerPogoHopperCounter;
			var showPlayerGalakrondCounter = WotogCounterHelper.ShowPlayerGalakrondCounter;
			var showPlayerLibramCounter = WotogCounterHelper.ShowPlayerLibramCounter;
			if(showPlayerCthunCounter)
			{
				var proxy = WotogCounterHelper.PlayerCthunProxy;
				WotogIconsPlayer.Attack = (proxy?.Attack ?? 6).ToString();
				WotogIconsPlayer.Health = (proxy?.Health ?? 6).ToString();
			}
			if(showPlayerSpellsCounter)
				WotogIconsPlayer.Spells = _game.Player.SpellsPlayedCount.ToString();
			if(showPlayerJadeCounter)
				WotogIconsPlayer.Jade = WotogCounterHelper.PlayerNextJadeGolem.ToString();
			if(showPlayerPogoHopperCounter)
				WotogIconsPlayer.PogoHopper = ((_game.Player.PogoHopperPlayedCount + 1 ) * 2 - 1).ToString();
			if(showPlayerGalakrondCounter)
				WotogIconsPlayer.Galakrond = WotogCounterHelper.PlayerGalakrondInvokeCounter.ToString();
			if(showPlayerLibramCounter)
				WotogIconsPlayer.Libram = WotogCounterHelper.PlayerLibramCounter.ToString();
			WotogIconsPlayer.WotogCounterStyle = showPlayerCthunCounter && showPlayerSpellsCounter ? Full : (showPlayerCthunCounter ? Cthun : (showPlayerSpellsCounter ? Spells : None));
			WotogIconsPlayer.JadeCounterStyle = showPlayerJadeCounter ? Full : None;
			WotogIconsPlayer.PogoHopperCounterStyle = showPlayerPogoHopperCounter ? Full : None;
			WotogIconsPlayer.GalakrondCounterStyle = showPlayerGalakrondCounter ? Full : None;
			WotogIconsPlayer.LibramCounterStyle = showPlayerLibramCounter ? Full : None;

			var showOpponentCthunCounter = WotogCounterHelper.ShowOpponentCthunCounter;
			var showOpponentSpellsCounter = WotogCounterHelper.ShowOpponentSpellsCounter;
			var showOpponentJadeCounter = WotogCounterHelper.ShowOpponentJadeCounter;
			var showOpponentPogoHopperCounter = WotogCounterHelper.ShowOpponentPogoHopperCounter;
			var showOpponentGalakrondCounter = WotogCounterHelper.ShowOpponentGalakrondCounter;
			var showOpponentLibramCounter = WotogCounterHelper.ShowOpponentLibramCounter;
			if(showOpponentCthunCounter)
			{
				var proxy = WotogCounterHelper.OpponentCthunProxy;
				WotogIconsOpponent.Attack = (proxy?.Attack ?? 6).ToString();
				WotogIconsOpponent.Health = (proxy?.Health ?? 6).ToString();
			}
			if(showOpponentSpellsCounter)
				WotogIconsOpponent.Spells = _game.Opponent.SpellsPlayedCount.ToString();
			if(showOpponentJadeCounter)
				WotogIconsOpponent.Jade = WotogCounterHelper.OpponentNextJadeGolem.ToString();
			if (showOpponentPogoHopperCounter)
				WotogIconsOpponent.PogoHopper = ((_game.Opponent.PogoHopperPlayedCount + 1) * 2 - 1).ToString();
			if(showOpponentGalakrondCounter)
				WotogIconsOpponent.Galakrond = WotogCounterHelper.OpponentGalakrondInvokeCounter.ToString();
			if(showOpponentLibramCounter)
				WotogIconsOpponent.Libram = WotogCounterHelper.OpponentLibramCounter.ToString();
			WotogIconsOpponent.WotogCounterStyle = showOpponentCthunCounter && showOpponentSpellsCounter ? Full : (showOpponentCthunCounter ? Cthun : (showOpponentSpellsCounter ? Spells : None));
			WotogIconsOpponent.JadeCounterStyle = showOpponentJadeCounter ? Full : None;
			WotogIconsOpponent.PogoHopperCounterStyle = showOpponentPogoHopperCounter ? Full : None;
			WotogIconsOpponent.GalakrondCounterStyle = showOpponentGalakrondCounter ? Full : None;
			WotogIconsOpponent.LibramCounterStyle = showOpponentLibramCounter ? Full : None;
		}

		public void UpdatePosition()
		{
			//hide the overlay depenting on options
			ShowOverlay(
						!((Config.Instance.HideInBackground && !User32.IsHearthstoneInForeground())
						  || (Config.Instance.HideOverlayInSpectator && _game.CurrentGameMode == GameMode.Spectator) || Config.Instance.HideOverlay
						  || ForceHidden || Helper.GameWindowState == WindowState.Minimized));


			var hsRect = User32.GetHearthstoneRect(true);

			//hs window has height 0 if it just launched, screwing things up if the tracker is started before hs is. 
			//this prevents that from happening. 
			if(hsRect.Height == 0 || (Visibility != Visible && Core.Windows.CapturableOverlay == null))
				return;

			var prevWidth = Width;
			var prevHeight = Height;
			SetRect(hsRect.Top, hsRect.Left, hsRect.Width, hsRect.Height);
			if(Width != prevWidth)
				OnPropertyChanged(nameof(BoardWidth));
			if(Height != prevHeight)
			{
				OnPropertyChanged(nameof(BoardHeight));
				OnPropertyChanged(nameof(MinionWidth));
				OnPropertyChanged(nameof(CardWidth));
				OnPropertyChanged(nameof(CardHeight));
				OnPropertyChanged(nameof(MercAbilityHeight));
				OnPropertyChanged(nameof(MinionMargin));
				for(int i = 0; i < OppBoard.Count; i++)
				{
					OppBoard[i].Width = MinionWidth;
					OppBoard[i].Height = BoardHeight;
					OppBoard[i].Margin = MinionMargin;
				}

				for(int i = 0; i < PlayerBoard.Count; i++)
				{
					PlayerBoard[i].Width = MinionWidth;
					PlayerBoard[i].Height = BoardHeight;
					PlayerBoard[i].Margin = MinionMargin;
				}
			}

			UpdateElementSizes();
			ApplyAutoScaling();
			UpdateElementPositions();

			try
			{
				if(Visibility == Visible)
				{
					UpdateCardTooltip();
					UpdateBattlegroundsOverlay();
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
		}

		internal void UpdateTurnTimer(TimerState timerState)
		{
			if((timerState.PlayerSeconds <= 0 && timerState.OpponentSeconds <= 0) || _game.CurrentMode != Mode.GAMEPLAY)
				return;
			ShowTimers();
			var seconds = (int)Math.Abs(timerState.Seconds);
			LblTurnTime.Text = double.IsPositiveInfinity(timerState.Seconds) ? "\u221E" : $"{seconds / 60 % 60:00}:{seconds % 60:00}";
			LblTurnTime.Fill = timerState.Seconds < 0 ? Brushes.LimeGreen : Brushes.White;
			LblPlayerTurnTime.Text = $"{timerState.PlayerSeconds / 60 % 60:00}:{timerState.PlayerSeconds % 60:00}";
			LblOpponentTurnTime.Text = $"{timerState.OpponentSeconds / 60 % 60:00}:{timerState.OpponentSeconds % 60:00}";
		}

		public void UpdateScaling()
		{
			StackPanelPlayer.RenderTransform = new ScaleTransform(Config.Instance.OverlayPlayerScaling / 100,
																  Config.Instance.OverlayPlayerScaling / 100);
			StackPanelOpponent.RenderTransform = new ScaleTransform(Config.Instance.OverlayOpponentScaling / 100,
																	Config.Instance.OverlayOpponentScaling / 100);
			StackPanelSecrets.RenderTransform = new ScaleTransform(Config.Instance.SecretsPanelScaling, Config.Instance.SecretsPanelScaling);
			LinkOpponentDeckDisplay.RenderTransform = new ScaleTransform(Config.Instance.OverlayOpponentScaling / 100,
																	Config.Instance.OverlayOpponentScaling / 100);
		}

		public double AutoScaling { get; set; } = 1;

		private void UpdateBoardPosition()
		{
			var step = _game.GameEntity?.GetTag(STEP);
			var isMainAction = step == (int)Step.MAIN_ACTION || step == (int)Step.MAIN_POST_ACTION || step == (int)Step.MAIN_PRE_ACTION;
			var mercsToNominate = _game.GameEntity?.HasTag(ALLOW_MOVE_MINION) ?? false;
			var opponentBoardOffset = _game.IsMercenariesMatch && isMainAction && !mercsToNominate ? Height * 0.142 : Height * 0.045;
			Canvas.SetTop(GridOpponentBoard, Height / 2 - GridOpponentBoard.ActualHeight - opponentBoardOffset);

			var playerBoardOffset = _game.IsMercenariesMatch ? isMainAction && !mercsToNominate ? Height * -0.09 : Height * 0.003 : Height * 0.03 ;
			Canvas.SetTop(GridPlayerBoard, Height / 2 - playerBoardOffset);
		}

		private void UpdateElementPositions()
		{
			var BorderStackPanelOpponentTop = Height * Config.Instance.OpponentDeckTop / 100;

			Canvas.SetTop(BorderStackPanelPlayer, Height * Config.Instance.PlayerDeckTop / 100);
			Canvas.SetLeft(BorderStackPanelPlayer, Width * Config.Instance.PlayerDeckLeft / 100 - StackPanelPlayer.ActualWidth * Config.Instance.OverlayPlayerScaling / 100);
			Canvas.SetTop(BorderStackPanelOpponent, BorderStackPanelOpponentTop);
			Canvas.SetLeft(BorderStackPanelOpponent, Width * Config.Instance.OpponentDeckLeft / 100);
			Canvas.SetTop(StackPanelSecrets, Height * Config.Instance.SecretsTop / 100);
			Canvas.SetLeft(StackPanelSecrets, Width * Config.Instance.SecretsLeft / 100);
			Canvas.SetTop(LblTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 - 5);
			Canvas.SetLeft(LblTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100);
			Canvas.SetTop(LblOpponentTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 - Config.Instance.TimersVerticalSpacing);
			Canvas.SetLeft(LblOpponentTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100 + Config.Instance.TimersHorizontalSpacing);
			Canvas.SetTop(LblPlayerTurnTime, Height * Config.Instance.TimersVerticalPosition / 100 + Config.Instance.TimersVerticalSpacing);
			Canvas.SetLeft(LblPlayerTurnTime, Width * Config.Instance.TimersHorizontalPosition / 100 + Config.Instance.TimersHorizontalSpacing);
			Canvas.SetTop(WotogIconsPlayer, Height * Config.Instance.WotogIconsPlayerVertical / 100);
			Canvas.SetLeft(WotogIconsPlayer, Helper.GetScaledXPos(Config.Instance.WotogIconsPlayerHorizontal / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(WotogIconsOpponent, Height * Config.Instance.WotogIconsOpponentVertical / 100);
			Canvas.SetLeft(WotogIconsOpponent, Helper.GetScaledXPos(Config.Instance.WotogIconsOpponentHorizontal / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(IconBoardAttackPlayer, Height * Config.Instance.AttackIconPlayerVerticalPosition / 100);
			Canvas.SetLeft(IconBoardAttackPlayer, Helper.GetScaledXPos(Config.Instance.AttackIconPlayerHorizontalPosition / 100, (int)Width, ScreenRatio));
			Canvas.SetTop(IconBoardAttackOpponent, Height * Config.Instance.AttackIconOpponentVerticalPosition / 100);
			Canvas.SetLeft(IconBoardAttackOpponent, Helper.GetScaledXPos(Config.Instance.AttackIconOpponentHorizontalPosition / 100, (int)Width, ScreenRatio));
			UpdateBoardPosition();

			Canvas.SetLeft(LinkOpponentDeckDisplay, Width * Config.Instance.OpponentDeckLeft / 100);

			var OpponentStackVisibleHeight = (CanvasOpponentCount.ActualHeight + CanvasOpponentChance.ActualHeight + ViewBoxOpponent.ActualHeight)* Config.Instance.OverlayOpponentScaling / 100;

			if(BorderStackPanelOpponentTop + OpponentStackVisibleHeight + 10 + LinkOpponentDeckDisplay.ActualHeight < Height)
			{
				Canvas.SetTop(LinkOpponentDeckDisplay, BorderStackPanelOpponentTop + OpponentStackVisibleHeight + 10);
			}
			else
			{
				Canvas.SetTop(LinkOpponentDeckDisplay, BorderStackPanelOpponentTop - (LinkOpponentDeckDisplay.ActualHeight* Config.Instance.OverlayOpponentScaling / 100) - 10);
			}

			if (Config.Instance.ShowFlavorText)
			{
				Canvas.SetTop(GridFlavorText, Height - GridFlavorText.ActualHeight - 10);
				Canvas.SetLeft(GridFlavorText, Width - GridFlavorText.ActualWidth - 10);
			}
			var handCount = _game.Opponent.HandCount > 10 ? 10 : _game.Opponent.HandCount;
			var opponentScaling = Config.Instance.OverlayOpponentScaling / 100;
			var opponentOpacity = Config.Instance.OpponentOpacity / 100;
			for (var i = 0; i < handCount; i++)
			{
				_cardMarks[i].Opacity = opponentOpacity;
				_cardMarks[i].ScaleTransform = new ScaleTransform(opponentScaling, opponentScaling);
				var width = _cardMarks[i].Width * Config.Instance.OverlayOpponentScaling / 100;
				var height = _cardMarks[i].Height * Config.Instance.OverlayOpponentScaling / 100;
				Canvas.SetLeft(_cardMarks[i], Helper.GetScaledXPos(_cardMarkPos[handCount - 1][i].X, (int)Width, ScreenRatio) - width / 2);
				Canvas.SetTop(_cardMarks[i], Math.Max(_cardMarkPos[handCount - 1][i].Y * Height - height / 3, 5));
			}
		}

		private double _wotogSize;
		private void UpdateElementSizes()
		{
			OnPropertyChanged(nameof(PlayerStackHeight));
			OnPropertyChanged(nameof(PlayerListHeight));
			OnPropertyChanged(nameof(OpponentStackHeight));
			OnPropertyChanged(nameof(OpponentListHeight));
			OnPropertyChanged(nameof(BattlegroundsTileHeight));
			OnPropertyChanged(nameof(BattlegroundsTileWidth));
			
			//Scale attack icons, with height
			var atkWidth = (int)Math.Round(Height * 0.0695, 0);
			var atkFont = (int)Math.Round(Height * 0.0204, 0);
			var atkFontMarginTop = (int)Math.Round(Height * 0.0038, 0);
			IconBoardAttackPlayer.Width = atkWidth;
			IconBoardAttackPlayer.Height = atkWidth;
			TextBlockPlayerAttack.Width = atkWidth;
			TextBlockPlayerAttack.Height = atkWidth;
			IconBoardAttackOpponent.Width = atkWidth;
			IconBoardAttackOpponent.Height = atkWidth;
			TextBlockOpponentAttack.Width = atkWidth;
			TextBlockOpponentAttack.Height = atkWidth;
			TextBlockPlayerAttack.Margin = new Thickness(0, atkFontMarginTop, 0, 0);
			TextBlockOpponentAttack.Margin = new Thickness(0, atkFontMarginTop, 0, 0);

			if(atkFont > 0)
			{
				TextBlockPlayerAttack.FontSize = atkFont;
				TextBlockOpponentAttack.FontSize = atkFont;
			}

			var wotogSize = Math.Min(1, Height / 1800);
			if(_wotogSize != wotogSize)
			{
				WotogIconsPlayer.RenderTransform = new ScaleTransform(wotogSize, wotogSize);
				WotogIconsOpponent.RenderTransform = new ScaleTransform(wotogSize, wotogSize);
				_wotogSize = wotogSize;
			}
		}

		public void ApplyAutoScaling()
		{
			AutoScaling = Math.Max(0.8, Math.Min(1.3, Height / 1080));

			_bgsTopBarBehavior.UpdatePosition();
			_bgsTopBarBehavior.UpdateScaling();

			_heroNotificationBehavior.UpdatePosition();
			_heroNotificationBehavior.UpdateScaling();

			_bgsBobsBuddyBehavior.UpdatePosition();
			_bgsBobsBuddyBehavior.UpdateScaling();

			_bgsPastOpponentBoardBehavior.UpdatePosition();
			_bgsPastOpponentBoardBehavior.UpdateScaling();

			_experienceCounterBehavior.UpdatePosition();
			_experienceCounterBehavior.UpdateScaling();

			_mercenariesTaskListButtonBehavior.UpdatePosition();
			_mercenariesTaskListButtonBehavior.UpdateScaling();

			_mercenariesTaskListBehavior.UpdatePosition();
			_mercenariesTaskListBehavior.UpdateScaling();
		}

		public void UpdateStackPanelAlignment()
		{
			OnPropertyChanged(nameof(PlayerStackPanelAlignment));
			OnPropertyChanged(nameof(OpponentStackPanelAlignment));
		}

		public void UpdateCardFrames()
		{
			CanvasOpponentChance.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasOpponentCount.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasPlayerChance.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
			CanvasPlayerCount.GetBindingExpression(Panel.BackgroundProperty)?.UpdateTarget();
		}

		private void OverlayWindow_OnDeactivated(object sender, EventArgs e) => SetTopmost();
	}
}
