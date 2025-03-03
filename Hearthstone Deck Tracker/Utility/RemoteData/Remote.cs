﻿using System.Collections.Generic;

namespace Hearthstone_Deck_Tracker.Utility.RemoteData
{
	internal static class Remote
	{
		public static DataLoader<RemoteData.Config> Config { get; }
			= DataLoader<RemoteData.Config>.JsonFromWeb("https://hsdecktracker.net/config.json");

		public static DataLoader<List<RemoteData.Mercenary>> Mercenaries { get; }
			= DataLoader<List<RemoteData.Mercenary>>.JsonFromWeb("https://hsdecktracker.net/data/mercs.json");
	}
}
