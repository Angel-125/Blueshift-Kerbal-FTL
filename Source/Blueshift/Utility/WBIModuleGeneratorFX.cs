﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.Localization;

/*
Source code copyright 2021, by Michael Billard (Angel-125)
License: GPLV3

Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace Blueshift
{
    /// <summary>
    /// An enhanced version of the stock ModuleGenerator that supports playing effects.
    /// </summary>
    public class WBIModuleGeneratorFX : ModuleResourceConverter, IModuleInfo
    {
        #region Fields
        /// <summary>
        /// A flag to enable/disable debug mode.
        /// </summary>
        [KSPField]
        public bool debugMode = false;

        /// <summary>
        /// The module's title/display name.
        /// </summary>
        [KSPField]
        public string moduleTitle = "Generator";

        /// <summary>
        /// The module's description.
        /// </summary>
        [KSPField]
        public string moduleDescription = "Produces and consumes resources";

        /// <summary>
        /// The ID of the part module. Since parts can have multiple generators, this field helps identify them.
        /// </summary>
        [KSPField]
        public string moduleID = string.Empty;

        /// <summary>
        /// Toggles visibility of the GUI.
        /// </summary>
        [KSPField]
        public bool guiVisible = true;

        /// <summary>
        /// Generators can control WBIAnimatedTexture modules. This field tells the generator which WBIAnimatedTexture to control.
        /// </summary>
        [KSPField]
        public string textureModuleID = string.Empty;

        /// <summary>
        /// A throttle control to vary the animation speed of a controlled WBIAnimatedTexture
        /// </summary>
        [KSPField(isPersistant = true, guiName = "Animation Throttle")]
        [UI_FloatRange(stepIncrement = 0.01f, maxValue = 1f, minValue = 0f)]
        public float animationThrottle = 1.0f;

        /// <summary>
        /// Generators can play a start effect when the generator is activated.
        /// </summary>
        [KSPField]
        public string startEffect = string.Empty;

        /// <summary>
        /// Generators can play a stop effect when the generator is deactivated.
        /// </summary>
        [KSPField]
        public string stopEffect = string.Empty;

        /// <summary>
        /// Generators can play a running effect while the generator is running.
        /// </summary>
        [KSPField]
        public string runningEffect = string.Empty;

        /// <summary>
        /// Name of the Waterfall effects controller that controls the warp effects (if any).
        /// </summary>
        [KSPField]
        public string waterfallEffectController = string.Empty;

        #region Maintenance
        /// <summary>
        /// Flag to indicate that the part needs maintenance in order to function.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool needsMaintenance = false;

        /// <summary>
        /// In hours, how long until the part needs maintenance in order to function. Default is 600.
        /// </summary>
        [KSPField]
        public double mtbf = 600;

        /// <summary>
        /// In seconds, the current time remaining until the part needs maintenance in order to function.
        /// </summary>
        [KSPField(isPersistant = true)]
        public double currentMTBF = 600 * 3600;

        /// <summary>
        /// The skill required to perform repairs. Default is "RepairSkill" (Engineers have this).
        /// </summary>
        [KSPField]
        public string repairSkill = "RepairSkill";

        /// <summary>
        /// The minimum skill level required to perform repairs. Default is 1.
        /// </summary>
        [KSPField]
        public int minimumSkillLevel = 1;

        /// <summary>
        /// The part name that is consumed during repairs. Default is "evaRepairKit" (the stock EVA Repair Kit).
        /// </summary>
        [KSPField]
        public string repairKitName = "evaRepairKit";

        /// <summary>
        /// The number of repair kits required to repair the part. Default is 1.
        /// </summary>
        [KSPField]
        public int repairKitsRequired = 1;
        #endregion

        #endregion

        #region Housekeeping
        /// <summary>
        /// Flag indicating whether or not we're missing resources needed to produce outputs.
        /// </summary>
        public bool isMissingResources = false;

        WBIAnimatedTexture[] animatedTextures = null;
        List<ResourceRatio> drainedResources = new List<ResourceRatio>();
        WFModuleWaterfallFX waterfallFXModule = null;
        #endregion

        #region IModuleInfo
        public string GetModuleTitle()
        {
            return ConverterName;
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        public string GetPrimaryField()
        {
            return string.Empty;
        }

        public override string GetModuleDisplayName()
        {
            return GetModuleTitle();
        }

        public override string GetInfo()
        {
            string info = base.GetInfo();

            StringBuilder builder = new StringBuilder();

            info = info.Replace(ConverterName, moduleDescription);
            builder.AppendLine(info);
            builder.AppendLine(Localizer.Format("#LOC_BLUESHIFT_infoMaintenance"));
            builder.AppendLine(Localizer.Format("#LOC_BLUESHIFT_infoMTB", new string[1] { string.Format("{0:n1}", mtbf) }));
            builder.AppendLine(Localizer.Format("#LOC_BLUESHIFT_infoRepairSkill", new string[1] { repairSkill }));
            builder.AppendLine(Localizer.Format("#LOC_BLUESHIFT_infoRepairRating", new string[1] { minimumSkillLevel.ToString() }));
            builder.AppendLine(Localizer.Format("#LOC_BLUESHIFT_infoKitsRequired", new string[1] { repairKitsRequired.ToString() }));

            return info;
        }
        #endregion

        #region Events
        /// <summary>
        /// Performs maintenance on the part.
        /// </summary>
        [KSPEvent(guiName = "#LOC_BLUESHIFT_repairPart", guiActiveUnfocused = true, unfocusedRange = 5)]
        public void RepairPart()
        {
            if (BlueshiftScenario.shared.CanRepairPart(repairSkill, minimumSkillLevel, repairKitName, repairKitsRequired))
            {
                BlueshiftScenario.shared.ConsumeRepairKits(FlightGlobals.ActiveVessel, repairKitName, repairKitsRequired);
                needsMaintenance = false;
                currentMTBF = mtbf * 3600;
                string message = Localizer.Format("#LOC_BLUESHIFT_partRepaired", new string[1] { part.partInfo.title });
                ScreenMessages.PostScreenMessage(message, BlueshiftScenario.messageDuration, ScreenMessageStyle.UPPER_CENTER);
                Events["RepairPart"].active = false;
            }
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) break part", guiActive = true)]
        public void DebugBreakPart()
        {
            needsMaintenance = false;
            currentMTBF = 1f;
        }
        #endregion

        #region Overrides
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasNode("DRAINED_RESOURCE"))
            {
                ConfigNode[] nodes = node.GetNodes("DRAINED_RESOURCE");
                double ratio = 0;
                bool dumpExcess = true;
                ResourceFlowMode flowMode = ResourceFlowMode.ALL_VESSEL;
                for (int index = 0; index < nodes.Length; index++)
                {
                    ConfigNode configNode = nodes[index];
                    if (!configNode.HasValue("ResourceName") && !configNode.HasValue("Ratio"))
                        continue;

                    ratio = 0;
                    dumpExcess = true;
                    double.TryParse(configNode.GetValue("Ratio"), out ratio);
                    bool.TryParse(configNode.GetValue("DumpExcess"), out dumpExcess);

                    ResourceRatio resource = new ResourceRatio(node.GetValue("ResourceName"), ratio, dumpExcess);

                    flowMode = ResourceFlowMode.ALL_VESSEL;
                    if (configNode.HasValue("FlowMode"))
                    {
                        flowMode = (ResourceFlowMode)Enum.Parse(typeof(ResourceFlowMode), configNode.GetValue("FlowMode"));
                        resource.FlowMode = flowMode;
                    }
                    else
                    {
                        resource.FlowMode = ResourceFlowMode.ALL_VESSEL;
                    }

                    drainedResources.Add(resource);
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (HighLogic.LoadedSceneIsEditor)
                this.DisableModule();
            else if (HighLogic.LoadedSceneIsFlight)
                this.EnableModule();

            // Setup GUI
            Fields["animationThrottle"].guiActive = debugMode;
            Fields["animationThrottle"].guiActiveEditor = debugMode;
            Events["DebugBreakPart"].active = debugMode;
            Events["DebugBreakPart"].guiName = "Break " + ClassName;
            Events["RepairPart"].active = needsMaintenance;
            Events["RepairPart"].guiName = Localizer.Format("#LOC_BLUESHIFT_repairPart", new string[1] { moduleTitle });
            if (needsMaintenance)
                status = Localizer.Format("#LOC_BLUESHIFT_needsMaintenance");

            // Get animated textures
            animatedTextures = getAnimatedTextureModules();

            // Get Waterfall module (if any)
            waterfallFXModule = WFModuleWaterfallFX.GetWaterfallModule(this.part);
        }

        public override void StartResourceConverter()
        {
            base.StartResourceConverter();

            this.part.Effect(startEffect, 1.0f);

            updateTextureModules();
        }

        public override void StopResourceConverter()
        {
            base.StopResourceConverter();

            this.part.Effect(runningEffect, 0.0f);
            this.part.Effect(stopEffect, 1.0f);

            updateTextureModules();

            if (waterfallFXModule != null)
            {
                waterfallFXModule.SetControllerValue(waterfallEffectController, 0);
            }
        }

        public override void OnInactive()
        {
            base.OnInactive();
            StopResourceConverter();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (!moduleIsEnabled)
                return;
            if (!IsActivated && !AlwaysActive)
            {
                // Drain any resources that we need to deplete.
                drainResources();
                return;
            }

            // Set animationThrottle
            if (isMissingResources)
            {
                drainResources();
                animationThrottle = 0f;
            }

            // Play running effect if needed.
            this.part.Effect(runningEffect, animationThrottle);

            // Update animated textures
            updateTextureModules();

            // Update Waterfall
            if (waterfallFXModule != null && !string.IsNullOrEmpty(waterfallEffectController))
            {
                waterfallFXModule.SetControllerValue(waterfallEffectController, animationThrottle);
            }
        }

        protected override ConversionRecipe PrepareRecipe(double deltatime)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return base.PrepareRecipe(deltatime);

            // Check maintenance
            if (BlueshiftScenario.maintenanceEnabled)
            {
                if (needsMaintenance)
                {
                    return null;
                }
                else
                {
                    currentMTBF -= deltatime;

                    if (currentMTBF <= 0)
                    {
                        status = Localizer.Format("#LOC_BLUESHIFT_needsMaintenance");
                        Events["RepairPart"].active = true;
                        needsMaintenance = true;
                        string message = Localizer.Format("#LOC_BLUESHIFT_partNeedsMaintenance", new string[1] { part.partInfo.title });
                        ScreenMessages.PostScreenMessage(message, BlueshiftScenario.messageDuration, ScreenMessageStyle.UPPER_LEFT);
                        return null;
                    }
                }
            }

            //Check resources
            int count = inputList.Count;
            double currentAmount, maxAmount;
            PartResourceDefinition resourceDefinition;
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;
            for (int index = 0; index < count; index++)
            {
                if (definitions.Contains(inputList[index].ResourceName))
                    resourceDefinition = definitions[inputList[index].ResourceName];
                else
                    continue;

                this.part.vessel.resourcePartSet.GetConnectedResourceTotals(resourceDefinition.id, out currentAmount, out maxAmount, true);

                if (currentAmount <= 0)
                {
                    status = "Missing " + resourceDefinition.displayName;
                    isMissingResources = true;
                    return null;
                }
            }

            isMissingResources = false;
            status = "Nominal";
            return base.PrepareRecipe(deltatime);
        }
        #endregion

        #region API
        /// <summary>
        /// Returns the amount of the supplied resource that is produced per second.
        /// </summary>
        /// <param name="resourceName">A string containing the name of the resource to look for.</param>
        /// <returns>A double containing the amount of the resource produced, or 0 if the resource can't be found.</returns>
        public double GetAmountProduced(string resourceName)
        {
            int count = outputList.Count;

            for (int index = 0; index < count; index++)
            {
                if (outputList[index].ResourceName == resourceName)
                    return outputList[index].Ratio;
            }

            return 0;
        }
        #endregion

        #region Helpers
        protected void drainResources()
        {
            int count = drainedResources.Count;
            ResourceRatio resource;

            for (int index = 0; index < count; index++)
            {
                resource = drainedResources[index];

                this.part.RequestResource(resource.ResourceName, resource.Ratio, resource.FlowMode);
            }
        }

        protected void updateTextureModules()
        {
            if (animatedTextures == null)
                return;

            for (int index = 0; index < animatedTextures.Length; index++)
            {
                animatedTextures[index].isActivated = IsActivated;
                animatedTextures[index].animationThrottle = animationThrottle;
            }
        }

        protected WBIAnimatedTexture[] getAnimatedTextureModules()
        {
            List<WBIAnimatedTexture> textureModules = this.part.FindModulesImplementing<WBIAnimatedTexture>();
            List<WBIAnimatedTexture> animationModules = new List<WBIAnimatedTexture>();
            int count = textureModules.Count;

            for (int index = 0; index < count; index++)
            {
                if (textureModules[index].moduleID == textureModuleID)
                    animationModules.Add(textureModules[index]);
            }

            return animationModules.ToArray();
        }
        #endregion
    }
}
