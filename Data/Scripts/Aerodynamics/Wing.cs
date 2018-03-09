﻿using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.Aerodynamics
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "WingAngled1", "WingAngled2", "WingStreight")]
    public class Wing : MyGameLogicComponent
    {
        private float atmosphere = 0;
        private int atmospheres = 0;
        private Vector3? debugPrevColor = null;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                var block = (IMyTerminalBlock)Entity;

                if(block.CubeGrid.Physics == null)
                {
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
                else
                {
                    NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;

                    block.ShowInTerminal = false;
                    block.ShowInToolbarConfig = false;
                    block.AppendingCustomInfo += CustomInfo;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            var block = (IMyTerminalBlock)Entity;
            block.AppendingCustomInfo -= CustomInfo;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!Aerodynamics.instance.enabled)
                    return;

                var block = (IMyTerminalBlock)Entity;
                var grid = (MyCubeGrid)block.CubeGrid;

                if(grid.Physics == null || grid.Physics.IsStatic || block.MarkedForClose || block.Closed || !block.IsWorking)
                    return;

                var gridCenter = grid.Physics.CenterOfMassWorld;

                bool debug = MyAPIGateway.Session.CreativeMode && block.ShowInToolbarConfig;
                bool debugText = true;
                IMySlimBlock slim = null;

                if(debug)
                {
                    slim = grid.GetCubeBlock(block.Position);

                    if(MyAPIGateway.Session.ControlledObject != null && MyAPIGateway.Session.ControlledObject.Entity != null)
                    {
                        var controlled = MyAPIGateway.Session.ControlledObject.Entity;
                        debugText = (controlled.EntityId == grid.EntityId || Vector3D.DistanceSquared(block.GetPosition(), controlled.GetPosition()) <= (30 * 30));
                    }

                    if(!debugPrevColor.HasValue)
                    {
                        debugPrevColor = slim.GetColorMask();
                    }
                }
                else if(debugPrevColor.HasValue)
                {
                    grid.ChangeColor(grid.GetCubeBlock(slim.Position), debugPrevColor.Value);
                    debugPrevColor = null;
                }

                if(atmospheres == 0 || atmosphere <= float.Epsilon)
                {
                    if(debug)
                    {
                        if(debugText)
                            MyAPIGateway.Utilities.ShowNotification(block.CustomName + ": not in atmosphere", 16, MyFontEnum.Red);

                        if(Vector3.DistanceSquared(slim.GetColorMask(), Aerodynamics.instance.DEBUG_COLOR_INACTIVE) > float.Epsilon)
                            grid.ChangeColor(grid.GetCubeBlock(slim.Position), Aerodynamics.instance.DEBUG_COLOR_INACTIVE);
                    }

                    return;
                }

                var blockMatrix = block.WorldMatrix;
                var vel = grid.Physics.GetVelocityAtPoint(blockMatrix.Translation);
                double speedSq = MathHelper.Clamp(vel.LengthSquared() * 2, 0, 10000);

                //if(debug)
                //{
                //    MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), (Color.YellowGreen * 0.5f), blockMatrix.Translation, Vector3D.Normalize(grid.Physics.LinearVelocity), 12, 0.1f);
                //}

                if(speedSq >= 50)
                {
                    Vector3D fw = blockMatrix.Left;
                    double forceMul = 0.75;

                    switch(block.BlockDefinition.SubtypeId)
                    {
                        case "WingAngled1":
                            forceMul = 1.0;
                            fw = Vector3D.Normalize(blockMatrix.Left + blockMatrix.Forward * 0.15);
                            break;
                        case "WingAngled2":
                            forceMul = 1.25;
                            fw = Vector3D.Normalize(blockMatrix.Left + blockMatrix.Forward * 0.35);
                            break;
                    }

                    double speedDir = fw.Dot(vel);

                    if(debug)
                    {
                        MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), ((speedDir > 0 ? Color.Blue : Color.Red) * 0.5f), blockMatrix.Translation, Vector3D.Normalize(vel), 10, 0.05f);
                    }

                    if(speedDir > 0)
                    {
                        var upDir = blockMatrix.Up;
                        var forceVector = -upDir * upDir.Dot(vel) * forceMul * speedSq * atmosphere;

                        grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceVector, gridCenter, null);

                        if(debug)
                        {
                            var totalForce = forceVector.Normalize();

                            var height = (float)totalForce / 100000f;
                            var pos = blockMatrix.Translation + forceVector * height;
                            MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("Square"), Color.Green * 0.5f, pos, blockMatrix.Forward, forceVector, 1.25f, height);

                            if(debugText)
                                MyAPIGateway.Utilities.ShowNotification(block.CustomName + ": forceMul=" + Math.Round(forceMul, 2) + "; atmosphere=" + Math.Round(atmosphere * 100, 0) + "%; totalforce=" + Math.Round(totalForce / 1000, 2) + " MN", 16, MyFontEnum.Green);

                            if(Vector3.DistanceSquared(slim.GetColorMask(), Aerodynamics.instance.DEBUG_COLOR_ACTIVE) > float.Epsilon)
                                grid.ChangeColor(grid.GetCubeBlock(slim.Position), Aerodynamics.instance.DEBUG_COLOR_ACTIVE);
                        }
                    }
                    else if(debug)
                    {
                        if(debugText)
                            MyAPIGateway.Utilities.ShowNotification(block.CustomName + ": wrong direction", 16, MyFontEnum.Red);

                        if(Vector3.DistanceSquared(slim.GetColorMask(), Aerodynamics.instance.DEBUG_COLOR_INACTIVE) > float.Epsilon)
                            grid.ChangeColor(grid.GetCubeBlock(slim.Position), Aerodynamics.instance.DEBUG_COLOR_INACTIVE);
                    }
                }
                else if(debug)
                {
                    if(debugText)
                        MyAPIGateway.Utilities.ShowNotification(block.CustomName + ": not enough speed", 16, MyFontEnum.Red);

                    if(Vector3.DistanceSquared(slim.GetColorMask(), Aerodynamics.instance.DEBUG_COLOR_INACTIVE) > float.Epsilon)
                        grid.ChangeColor(grid.GetCubeBlock(slim.Position), Aerodynamics.instance.DEBUG_COLOR_INACTIVE);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                if(!Aerodynamics.instance.enabled)
                    return;

                var block = (IMyTerminalBlock)Entity;
                var grid = (MyCubeGrid)block.CubeGrid;

                if(grid.Physics == null || grid.Physics.IsStatic || block.MarkedForClose || block.Closed || !block.IsWorking)
                    return;

                var gridCenter = grid.Physics.CenterOfMassWorld;
                var planets = Aerodynamics.instance.planets;

                atmosphere = 0;
                atmospheres = 0;

                for(int i = planets.Count - 1; i >= 0; --i)
                {
                    var planet = planets[i];

                    if(planet.Closed || planet.MarkedForClose)
                    {
                        planets.RemoveAt(i);
                        continue;
                    }

                    if(Vector3D.DistanceSquared(gridCenter, planet.WorldMatrix.Translation) < (planet.AtmosphereRadius * planet.AtmosphereRadius))
                    {
                        atmosphere += planet.GetAirDensity(gridCenter);
                        atmospheres++;
                    }
                }

                if(atmospheres > 0)
                {
                    atmosphere /= atmospheres;
                    atmosphere = MathHelper.Clamp((atmosphere - Aerodynamics.MIN_ATMOSPHERE) / (Aerodynamics.MAX_ATMOSPHERE - Aerodynamics.MIN_ATMOSPHERE), 0f, 1f);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            try
            {
                var block = (IMyTerminalBlock)Entity;
                var on = Aerodynamics.instance.enabled;

                block.RefreshCustomInfo();

                if(on)
                    NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME;
                else
                    NeedsUpdate &= ~(MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_10TH_FRAME);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void CustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            try
            {
                info.Append('\n');

                if(Aerodynamics.instance.enabled)
                    info.Append("Wings are enabled and working.");
                else
                    info.Append("Wings disabled by another mod: ").Append(Aerodynamics.instance.disabledBy ?? "(unknown mod)");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
