#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Drawing;
using System.Linq;
using OpenRA.Mods.RA.Move;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Mods.RA.Air;

namespace OpenRA.Mods.RA
{
	[Desc("This unit has access to build queues.")]
	public class ProductionInfo : ITraitInfo
	{
		[Desc("e.g. Infantry, Vehicles, Aircraft, Buildings")]
		public readonly string[] Produces = { };
		public readonly bool SpawnAtMapEdge = false;

		public virtual object Create(ActorInitializer init) { return new Production(this); }
	}

	[Desc("Where the unit should leave the building. Multiples are allowed if IDs are added: Exit@2, ...")]
	public class ExitInfo : TraitInfo<Exit>
	{
		[Desc("Offset at which that the exiting actor is spawned")]
		public readonly WVec SpawnOffset = WVec.Zero;

		[Desc("Cell offset where the exiting actor enters the ActorMap")]
		public readonly CVec ExitCell = CVec.Zero;
		public readonly int Facing = -1;
		public readonly bool MoveIntoWorld = true;
	}

	public class Exit { }

	public class Production
	{
		public ProductionInfo Info;
		public Production(ProductionInfo info)
		{
			Info = info;
		}

		public void DoProduction(Actor self, ActorInfo producee, ExitInfo exitinfo)
		{
			var exit = self.Location + exitinfo.ExitCell;
			var spawn = self.CenterPosition + exitinfo.SpawnOffset;
			var to = exit.CenterPosition;

			var fi = producee.Traits.Get<IFacingInfo>();
			var initialFacing = exitinfo.Facing < 0 ? Util.GetFacing(to - spawn, fi.GetInitialFacing()) : exitinfo.Facing;

			self.World.AddFrameEndTask(w =>
			{
				var newUnit = self.World.CreateActor(producee.Name, new TypeDictionary
				{
					new OwnerInit(self.Owner),
					new LocationInit(exit),
					new CenterPositionInit(spawn),
					new FacingInit(initialFacing)
				});

				var move = newUnit.Trait<IMove>();
				if (exitinfo.MoveIntoWorld)
					newUnit.QueueActivity(move.MoveIntoWorld(newUnit, exit));

				var target = MoveToRallyPoint(self, newUnit, exit);
				newUnit.SetTargetLine(Target.FromCell(target), Color.Green, false);
				foreach (var t in self.TraitsImplementing<INotifyProduction>())
					t.UnitProduced(self, newUnit, exit);
			});
		}

		[Desc("DoProduction Overload that takes a CPos to spawn at, instead of an exitinfo.")]
		public void DoProduction(Actor self, ActorInfo producee, CPos exitinfo)
		{

			var initialFacing = producee.Traits.Get<IFacingInfo>().GetInitialFacing();
			var facing = Util.GetFacing(self.CenterPosition - exitinfo.CenterPosition, initialFacing);
			WRange cruiseAltitude = WRange.Zero;
			if (producee.Traits.Contains<AircraftInfo>() == true)
			{
				cruiseAltitude = producee.Traits.Get<AircraftInfo>().CruiseAltitude;
			}

			self.World.AddFrameEndTask(w =>
			{
				var newUnit = self.World.CreateActor(producee.Name, new TypeDictionary
				{
					new OwnerInit(self.Owner),
					new LocationInit(exitinfo),
					new CenterPositionInit(new WPos(exitinfo.CenterPosition.X, exitinfo.CenterPosition.Y, cruiseAltitude.Range)),
					new FacingInit(facing)
				});

				var move = newUnit.Trait<IMove>();
				var exit = self.Location + self.Info.Traits.WithInterface<ExitInfo>().Shuffle(self.World.SharedRandom)
					.FirstOrDefault(e => CanUseExit(self, producee, e)).ExitCell;

				newUnit.QueueActivity(move.MoveIntoWorld(newUnit, WorldUtils.StepTowards(newUnit.Location, self.Location)));
				var target = MoveToRallyPoint(self, newUnit, exit);
				newUnit.SetTargetLine(Target.FromCell(target), Color.Green, false);

				foreach (var t in self.TraitsImplementing<INotifyProduction>())
					t.UnitProduced(self, newUnit, exit);
			});
		}

		static CPos MoveToRallyPoint(Actor self, Actor newUnit, CPos exitLocation)
		{
			var rp = self.TraitOrDefault<RallyPoint>();
			if (rp == null)
				return exitLocation;

			var move = newUnit.TraitOrDefault<IMove>();
			if (move != null)
			{
				newUnit.QueueActivity(new AttackMove.AttackMoveActivity(
					newUnit, move.MoveTo(rp.rallyPoint, rp.nearEnough)));
				return rp.rallyPoint;
			}

			return exitLocation;
		}

		public virtual bool Produce(Actor self, ActorInfo producee)
		{
			if (Reservable.IsReserved(self))
				return false;

			// pick a spawn/exit point pair
			var exit = self.Info.Traits.WithInterface<ExitInfo>().Shuffle(self.World.SharedRandom)
				.FirstOrDefault(e => CanUseExit(self, producee, e));

			if (exit != null)
			{
				if ( self.Trait<Production>().Info.SpawnAtMapEdge == true ) {
					DoProduction(self, producee, WorldUtils.NearestMapEdge(self.World, self.Location));
					return true;
				}
				DoProduction(self, producee, exit);
				return true;
			}

			return false;
		}

		static bool CanUseExit(Actor self, ActorInfo producee, ExitInfo s)
		{
			var mobileInfo = producee.Traits.GetOrDefault<MobileInfo>();

			foreach (var blocker in self.World.ActorMap.GetUnitsAt(self.Location + s.ExitCell))
			{
				// Notify the blocker that he's blocking our move:
				foreach (var moveBlocked in blocker.TraitsImplementing<INotifyBlockingMove>())
					moveBlocked.OnNotifyBlockingMove(blocker, self);
			}

			return mobileInfo == null ||
				mobileInfo.CanEnterCell(self.World, self, self.Location + s.ExitCell, self, true, true);
		}
	}
}
