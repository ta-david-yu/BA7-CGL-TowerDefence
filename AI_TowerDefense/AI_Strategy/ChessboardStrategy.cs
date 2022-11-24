using GameFramework;
using System;
using System.Collections.Generic;

namespace AI_Strategy
{
	public sealed class ChessboardStrategy : AbstractStrategy
	{
		public ChessboardStrategy(Player player) : base(player)
		{
		}

		public override void DeploySoldiers()
		{
		}

		public override void DeployTowers()
		{
		}

		public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
		{
			return unsortedList;
		}
	}
}
