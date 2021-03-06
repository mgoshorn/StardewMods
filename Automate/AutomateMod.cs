﻿using System;
using System.Collections.Generic;
using System.Linq;
using Pathoschild.Stardew.Automate.Framework;
using Pathoschild.Stardew.Common;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Pathoschild.Stardew.Automate
{
    /// <summary>The mod entry point.</summary>
    public class AutomateMod : Mod
    {
        /*********
        ** Properties
        *********/
        /// <summary>The mod configuration.</summary>
        private ModConfig Config;

        /// <summary>Constructs machine instances.</summary>
        private readonly MachineFactory Factory = new MachineFactory();

        /// <summary>The machines to process.</summary>
        private readonly IDictionary<GameLocation, MachineMetadata[]> Machines = new Dictionary<GameLocation, MachineMetadata[]>();

        /// <summary>Whether machines are initialised.</summary>
        private bool IsReady => Context.IsWorldReady && this.Machines.Any();


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides methods for interacting with the mod directory, such as read/writing a config file or custom JSON files.</param>
        public override void Entry(IModHelper helper)
        {
            // read config
            this.Config = helper.ReadConfig<ModConfig>();

            // hook events
            SaveEvents.AfterLoad += this.SaveEvents_AfterLoad;
            LocationEvents.LocationsChanged += this.LocationEvents_LocationsChanged;
            LocationEvents.LocationObjectsChanged += this.LocationEvents_LocationObjectsChanged;
            GameEvents.OneSecondTick += this.GameEvents_OneSecondTick;
        }


        /*********
        ** Private methods
        *********/
        /****
        ** Event handlers
        ****/
        /// <summary>The method invoked when the player loads a save.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            // check for updates
            if (this.Config.CheckForUpdates)
                UpdateHelper.LogVersionCheckAsync(this.Monitor, this.ModManifest, "Automate");
        }

        /// <summary>The method invoked when a location is added or removed.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void LocationEvents_LocationsChanged(object sender, EventArgsGameLocationsChanged e)
        {
            try
            {
                this.ReloadAllMachines();
            }
            catch (Exception ex)
            {
                this.HandleError(ex, "updating locations");
            }
        }

        /// <summary>The method invoked when an object is added or removed to a location.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void LocationEvents_LocationObjectsChanged(object sender, EventArgsLocationObjectsChanged e)
        {
            try
            {
                this.ReloadMachinesIn(Game1.currentLocation);
            }
            catch (Exception ex)
            {
                this.HandleError(ex, "updating the current location");
            }
        }

        /// <summary>The method invoked when the in-game clock time changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void GameEvents_OneSecondTick(object sender, EventArgs e)
        {
            if (!this.IsReady)
                return;

            try
            {
                foreach (MachineMetadata[] machines in this.Machines.Values)
                    this.ProcessMachines(machines);
            }
            catch (Exception ex)
            {
                this.HandleError(ex, "processing machines");
            }
        }

        /****
        ** Methods
        ****/
        /// <summary>Reload all machines.</summary>
        private void ReloadAllMachines()
        {
            this.Machines.Clear();
            foreach (GameLocation location in this.Factory.GetLocationsWithChests())
                this.ReloadMachinesIn(location);
        }

        /// <summary>Reload the machines in a given location.</summary>
        /// <param name="location">The location whose location to reload.</param>
        private void ReloadMachinesIn(GameLocation location)
        {
            this.Machines[location] = this.Factory.GetMachinesIn(location, this.Helper.Reflection).ToArray();
        }

        /// <summary>Process a set of machines.</summary>
        /// <param name="machines">The machines to process.</param>
        private void ProcessMachines(MachineMetadata[] machines)
        {
            foreach (MachineMetadata metadata in machines)
            {
                IMachine machine = metadata.Machine;

                switch (machine.GetState())
                {
                    case MachineState.Empty:
                        machine.Pull(metadata.Connected);
                        break;

                    case MachineState.Done:
                        metadata.Connected.TryPush(machine.GetOutput());
                        break;
                }
            }
        }

        /// <summary>Log an error and warn the user.</summary>
        /// <param name="ex">The exception to handle.</param>
        /// <param name="verb">The verb describing where the error occurred (e.g. "looking that up").</param>
        private void HandleError(Exception ex, string verb)
        {
            this.Monitor.Log($"Something went wrong {verb}:\n{ex}", LogLevel.Error);
            CommonHelper.ShowErrorMessage($"Huh. Something went wrong {verb}. The error log has the technical details.");
        }
    }
}
