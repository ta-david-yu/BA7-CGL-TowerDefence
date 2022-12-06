using GameFramework;
using System;
using System.Collections.Generic;

namespace AI_Strategy
{
	namespace DYStrategy
	{
		public sealed class ChessboardStrategy : AbstractStrategy
		{
			private static Random random = new Random();

			/// <summary>
			/// If there are cells without towers under this height, we spend money on building them first!
			/// </summary>
			private const int k_DangerTowerPlacementHeight = 2;

			/// <summary>
			/// We dont create more soldiers if there are more soliders than the value already.
			/// </summary>
			private const int k_MaxAllySoldierCount = 80;

			/// <summary>
			/// We dont build more towers if the tower defense line is further than the height here.
			/// </summary>
			private const int k_MaxTowerPlacementHeight = 3;

			public ChessboardStrategy(Player player) : base(player)
			{
			}

			public override void DeploySoldiers()
			{
				if (hasAvailableCellForTowerWithinHeight(k_DangerTowerPlacementHeight, out Cell firstPriorityTowerLocation))
				{
					return;
				}

				// Don't spawn more soldiers if the number has already reached the hard limit.
				// Also stop spawning more soldiers if the try-buy-times is bigger than the width.
				int previousPickedLaneX = random.Next(0, PlayerLane.WIDTH);
				int tryBuyTimes = 0;
				while (player.EnemyLane.SoldierCount() < k_MaxAllySoldierCount && tryBuyTimes < PlayerLane.WIDTH)
				{
					int bestLaneX = getPreferredEnemyLaneToSpawnSoldier(previousPickedLaneX);
					var result = player.TryBuySoldier<MySoldier>(bestLaneX);

					if (result == Player.SoldierPlacementResult.Success)
					{
						previousPickedLaneX = bestLaneX;
					}

					tryBuyTimes++;
				}

				if (player.EnemyLane.SoldierCount() < k_MaxAllySoldierCount)
				{
					for (int x = 0; x < PlayerLane.WIDTH; x++)
					{
						if (player.EnemyLane.GetCellAt(x, 0).Unit != null)
						{
							continue;
						}

						player.TryBuySoldier<MySoldier>(x);
						if (player.EnemyLane.SoldierCount() >= k_MaxAllySoldierCount)
						{
							// Don't spawn more if the number has already reached the hard limit.
							break;
						}
					}
				}
			}

			public override void DeployTowers()
			{
				if (player.Gold < Tower.GetNextTowerCosts(player.HomeLane))
				{
					// Not enough gold.
					return;
				}

				int towerPlaceableBoundsHeight = PlayerLane.HEIGHT - PlayerLane.HEIGHT_OF_SAFETY_ZONE;
				int maxHeight = k_MaxTowerPlacementHeight < towerPlaceableBoundsHeight ? k_MaxTowerPlacementHeight : towerPlaceableBoundsHeight;

				// I am more used to taking bottom-left corner as the origin, so the bottom-left corner has the coordinate of (0, 0).
				for (int y = 0; y < maxHeight; y++)
				{
					int translatedY = PlayerLane.HEIGHT - y - 1;
					bool isYEven = y % 2 == 0;
					int startX = isYEven ? 0 : 1;
					for (int x = startX; x < PlayerLane.WIDTH; x += 2)
					{
						var result = player.TryBuyTower<Tower>(x, translatedY);

						if (player.Gold < Tower.GetNextTowerCosts(player.HomeLane))
						{
							break;
						}
					}

					if (player.Gold < Tower.GetNextTowerCosts(player.HomeLane))
					{
						break;
					}
				}
			}

			public override List<Soldier> SortedSoldierArray(List<Soldier> unsortedList)
			{
				return unsortedList;
			}

			private bool hasAvailableCellForTowerWithinHeight(int height, out Cell firstAvailableCell)
			{
				int towerPlaceableBoundsHeight = PlayerLane.HEIGHT - PlayerLane.HEIGHT_OF_SAFETY_ZONE;
				int maxHeight = height < towerPlaceableBoundsHeight ? height : towerPlaceableBoundsHeight;

				// I am more used to taking bottom-left corner as the origin, so the bottom-left corner has the coordinate of (0, 0).
				for (int y = 0; y < maxHeight; y++)
				{
					int translatedY = PlayerLane.HEIGHT - y - 1;
					bool isYEven = y % 2 == 0;
					int startX = isYEven ? 0 : 1;
					for (int x = startX; x < PlayerLane.WIDTH; x += 2)
					{
						Cell cell = player.HomeLane.GetCellAt(x, translatedY);
						if (cell.Unit == null)
						{
							firstAvailableCell = cell;
							return true;
						}
					}
				}

				firstAvailableCell = null;
				return false;
			}

			private int getPreferredEnemyLaneToSpawnSoldier(int previousPickedLaneX)
			{
				const float adjacentDangerValueMultiplier = 0.5f;

				// TODO: pick the lane with the most soldiers as well.
				float[] laneInterestValues = new float[PlayerLane.WIDTH];

				float[] laneDangerValues = new float[PlayerLane.WIDTH];
				float lowestDangerValue = float.MaxValue;

				int towerPlaceableBoundsHeight = PlayerLane.HEIGHT - PlayerLane.HEIGHT_OF_SAFETY_ZONE;
				for (int x = 0; x < PlayerLane.WIDTH; x += 2)
				{
					for (int y = 0; y < towerPlaceableBoundsHeight; y++)
					{
						int translatedY = PlayerLane.HEIGHT - y - 1;

						Cell cell = player.EnemyLane.GetCellAt(x, translatedY);
						if (cell.Unit is Tower)
						{

							Tower tower = cell.Unit as Tower;

							// There is a tower at lane x, increase the lane danger value and the adjacent lane.
							laneDangerValues[x] += tower.Health;
							if (laneDangerValues[x] < lowestDangerValue)
							{
								lowestDangerValue = laneDangerValues[x];
							}

							if (x - 1 >= 0)
							{
								laneDangerValues[x - 1] += tower.Health * adjacentDangerValueMultiplier;
								if (laneDangerValues[x - 1] < lowestDangerValue)
								{
									lowestDangerValue = laneDangerValues[x - 1];
								}
							}

							if (x + 1 < PlayerLane.WIDTH)
							{
								laneDangerValues[x + 1] += tower.Health * adjacentDangerValueMultiplier;
								if (laneDangerValues[x + 1] < lowestDangerValue)
								{
									lowestDangerValue = laneDangerValues[x + 1];
								}
							}
						}
						else if (cell.Unit is Soldier)
						{
							Soldier soldier = cell.Unit as Soldier;

							// There is a soldier at lane x, increase the interest value by 1.
							laneInterestValues[x] += 1;
						}
					}
				}

				// We have the lowest danger value recorded, now we just need to pick a random slot with the value.
				float highestInterestValue = int.MinValue;
				int highestInterestLaneX = previousPickedLaneX;

				int startX = 0;// random.Next(0, PlayerLane.WIDTH);
				for (int offset = 0; offset < PlayerLane.WIDTH; offset++)
				{
					int x = (startX + offset) % PlayerLane.WIDTH;
					if (laneDangerValues[x] == lowestDangerValue)
					{
						if (laneInterestValues[x] > highestInterestValue)
						{
							highestInterestValue = laneInterestValues[x];
							highestInterestLaneX = x;
						}
					}
				}

				// Pick the lane with the lowest danger value & the highest interest value.
				return highestInterestLaneX;
			}
		}
	}
}
