﻿using System;
using System.Collections.Generic;
using Com.CodeGame.CodeTanks2012.DevKit.CSharpCgdk.Model;
using System.IO;
using System.Linq;

class TwoTankskActualStrategy : ActualStrategy
{
	Tank teammate;
	OneTankActualStrategy myOtherSelf = new OneTankActualStrategy();

	override public void Move(Tank self, World world, Move move)
	{
		myOtherSelf.historyX[world.Tick] = self.X;
		myOtherSelf.historyY[world.Tick] = self.Y;

		if (IsDead(teammates[0]))
		{
			myOtherSelf.CommonMove(self, world, move);
			return;
		}
		teammate = teammates[0];

		bool forward;
		Bonus bonus = GetBonus(out forward);

#if TEDDY_BEARS
		//bonus = null;
#endif
		bool shootOnlyToVictim = false;
		cornerX = cornerY = -1;
		if (bonus != null && (world.Tick > runToCornerTime || bonus.Type == BonusType.AmmoCrate))
		{
			MoveToBonus(bonus, forward);
		}
		else
		{
			MoveBackwards();
		}

		Tank victim = GetVictim();//GetWithSmallestDistSum();
		if (victim != null)
			TurnToMovingTank(victim, false);

		TryShoot(victim, shootOnlyToVictim);

		/*if (world.Tick > runToCornerTime && AliveEnemyCnt() <= 1)
		{
			var tank = GetMostAngryEnemy();
			if (tank != null)
				StayPerpendicular(tank);
		}*/

		if (AliveEnemyCnt() == 1)
		{
			Tank enemy = PickEnemy();
			if(MustRush(enemy))
				MoveTo(enemy, true);
		}

		bool bonusSaves = BonusSaves(self, bonus);

		if (world.Tick > runToCornerTime && victim != null && !HaveTimeToTurn(victim) && !bonusSaves)
			TurnToMovingTank(victim, true);
	}	

	int ShootToKill(Tank killer, Tank target)
	{
		int hd = target.HullDurability, ch = target.CrewHealth;
		int ans = 0;
		int ps = killer.PremiumShellCount;
		for (int i = 0; i < ps; i++)
		{
			ans++;
			hd -= 35;
			ch -= 35;
			if (hd <= 0 || ch <= 0)
				return ans;
		}
		while (hd > 0 && ch > 0)
		{
			ans++;
			hd -= 20;
			ch -= 10;
		}
		return ans;
	}

	bool MustRush(Tank enemy)
	{
		if (Math.Min(ShootToKill(enemy, self), ShootToKill(enemy, teammate)) <
		    Math.Max(ShootToKill(self, enemy), ShootToKill(teammate, enemy)))
			return false;

		double myDist = self.GetDistanceTo(enemy);
		double tmDist = teammate.GetDistanceTo(enemy);
		if (self.GetDistanceTo(enemy) > 4 * self.Width && !(myDist < tmDist - self.Width / 2))
			return true;
		return false;
	}

	bool Leftmost()
	{
		var a = enemies.OrderBy(tank => tank.X).ToArray();
		return a.Length != 0 && Math.Max(self.X, teammate.X) < a[0].X - 60;
	}

	bool Rightmost()
	{
		var a = enemies.OrderBy(tank => world.Width-tank.X).ToArray();
		return a.Length != 0 && Math.Min(self.X, teammate.X) > a[0].X + 60;
	}

	void MoveBackwards()
	{
		double firstX = self.Height*1.5;// nearest to vertical wall
		double firstY = self.Width*2.5; 
		double secondX = self.Height*3+15;
		double secondY = self.Width*1.5;
		double vertD = world.Height/4;
		double a = self.Width;
		if (Leftmost())
		{
			if (self.Y < teammate.Y)
				MoveToVert(a, vertD);
			else
				MoveToVert(a, world.Height -vertD);
		}
		else if (Rightmost())
		{
			if (self.Y < teammate.Y)
				MoveToVert(world.Width - a, vertD);
			else
				MoveToVert(world.Width - a, world.Height - vertD);
		}
		else
		{
			double x = (self.X + teammate.X) / 2;
			double y = (self.Y + teammate.Y) / 2;
			double bf = self.Width*2, bs = self.Width*4;
			if (x < world.Width / 2 && y < world.Height / 2)
			{
				if (self.X < teammate.X)
					MoveToHor(bf, a);
				else
					MoveToHor(bs, a);
			}
			else if (x < world.Width / 2 && y > world.Height / 2)
			{
				if (self.X < teammate.X)
					MoveToHor(bf, world.Height-a);
				else
					MoveToHor(bs, world.Height-a);
			}
			else if (x > world.Width / 2 && y < world.Height / 2)
			{
				if (self.X > teammate.X)
					MoveToHor(world.Width - bf, a);
				else
					MoveToHor(world.Width - bs, a);
			}
			else
			{
				if (self.X > teammate.X)
					MoveToHor(world.Width - bf, world.Height-a);
				else
					MoveToHor(world.Width - bs, world.Height-a);
			}
		}
	}

	/*protected override bool BadAim(Unit aim, Unit victim, bool shootOnlyToVictim, double x, double y, ShellType bulletType)
	{
		if (BadAim(aim, victim, shootOnlyToVictim, bulletType))
			return true;
		if (self.GetDistanceTo(aim) < self.Width * 3)
			return false;
		if (self.TeammateIndex == 0)
		{
			if (x < 0)
				return true;
		}
		else
		{
			if (x > 0)
				return true;
		}
		return false;
	}*/

	/*override protected void MoveBackwards(out double resX, out double resY)
	{
		resX = 0;
		resY = 0;
	}*/

	Tank GetWithSmallestDistSum()
	{
		double mi = inf;
		Tank res = null;
		foreach (var tank in world.Tanks)
		{
			if (tank.IsTeammate || IsDead(tank))
				continue;

			double test = self.GetDistanceTo(tank) + teammate.GetDistanceTo(tank);
			if (ObstacleBetween(self, tank, true))
				test = inf / 2;

			if (res == null || test < mi*0.8 || test < mi*1.2 && MoreSweet(tank,res))
			{
				mi = test;
				res = tank;
			}
		}
		return res;
	}

	

	/*static bool Inside(Unit unit, double x, double y, double precision, bool enemy = false)
	{
		double d = unit.GetDistanceTo(x, y);
		double angle = unit.GetAngleTo(x, y);
		x = d * Math.Cos(angle);
		y = d * Math.Sin(angle);
		double w = unit.Width / 2 + precision;
		double h = unit.Height / 2 + precision;
		double lx = -w, rx = w;
		double ly = -h, ry = h;
		if (enemy && Math.Sqrt(Util.Sqr(unit.SpeedX) + Util.Sqr(unit.SpeedY)) > 1)
		{
			if (IsMovingBackward(unit))
			{
				lx = 0;
			}
			else
			{
				rx = 0;
			}
		}
		return x >= lx && x <= rx &&
			y >= ly && y <= ry;
		//return x >= -w && x <= w &&
		//	   y >= -h && y <= h;
	}*/
}