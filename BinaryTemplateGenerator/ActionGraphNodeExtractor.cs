using System.Text.Json;
using System.Text.RegularExpressions;
using TrRebootTools.BinaryTemplateGenerator.Util;

namespace TrRebootTools.BinaryTemplateGenerator
{
    internal class ActionGraphNodeExtractor
    {
        private readonly TypeLibrary _lib;
        private readonly JsonDocument _slotsDoc;
        private readonly CdcGame _game;
        private readonly Dictionary<NodeType, CType> _nodeTypesByEnum = new();
        private readonly Dictionary<CType, CType> _argTypesByNodeType = new();

        private record Slot(string? Name, string Type);

        public ActionGraphNodeExtractor(TypeLibrary lib, JsonDocument slotsDoc, CdcGame game)
        {
            _lib = lib;
            _slotsDoc = slotsDoc;
            _game = game;
            FindNodeToArgMapping();
            FindEnumToNodeMapping();
        }

        private void FindNodeToArgMapping()
        {
            Dictionary<string, CType> argTypesByName = new();
            foreach (CStructure argsType in _lib.Types.Values.OfType<CStructure>())
            {
                if (!argsType.Name.StartsWith("dtp::") || argsType.BaseTypes.Length > 0)
                    continue;

                string normalizedName = NormalizeTypeName(argsType.Name);
                if (argTypesByName.TryGetValue(normalizedName, out CType? prevArgsType))
                {
                    if (argsType.Name.EndsWith("Node"))
                        argTypesByName[normalizedName] = argsType;

                    continue;
                }
                argTypesByName.Add(normalizedName, argsType);
            }

            foreach (CStructure nodeType in _lib.Types.Values.OfType<CStructure>())
            {
                if (!IsActionGraphNodeType(nodeType))
                    continue;

                CType? argsType = null;
                string? argsPointerTypeName = nodeType.Fields
                                                      .Select(f => f.Type)
                                                      .FirstOrDefault(t => t.StartsWith("dtp::") && t.EndsWith('*'));
                if (argsPointerTypeName != null)
                {
                    argsType = _lib.Types[argsPointerTypeName.TrimEnd('*').Trim()];
                }
                else
                {
                    argTypesByName.TryGetValue(NormalizeTypeName(nodeType.Name), out argsType);
                }

                if (argsType == null)
                    continue;

                _argTypesByNodeType.Add(nodeType, argsType);
            }
        }

        private void FindEnumToNodeMapping()
        {
            HashSet<string> registeredNodeTypeNames = new();
            foreach (CType registryType in _lib.Types.Values.OfType<CStructure>())
            {
                Match match = Regex.Match(registryType.Name, @"^ActionGraphFactory::Registry<\d+,(\w+),\d+>$");
                if (match.Success)
                    registeredNodeTypeNames.Add(match.Groups[1].Value);
            }

            foreach ((CType nodeType, CType argsType) in _argTypesByNodeType)
            {
                if (!registeredNodeTypeNames.Contains(nodeType.Name))
                    continue;

                for (int duplicateIdx = 0; duplicateIdx <= 1; duplicateIdx++)
                {
                    string enumName = nodeType.Name switch
                    {
                        "AGInterpNode<dtp::InterpVectorNode,AGVector3>" => "AGInterpVectorsNode",
                        "AGInterpNode<dtp::InterpFloatNode,float>" => "AGInterpFloatsNode",
                        "AGInterpNode<dtp::InterpIntNode,int>" => "AGInterpIntsNode",
                        "AGInterpNode<dtp::InterpColorNode,unsigned int>" => "AGInterpColorsNode",
                        _ => nodeType.Name
                    };
                    enumName += new string('_', duplicateIdx);
                    if (!Enum.TryParse(enumName, out NodeType enumValue))
                        break;

                    _nodeTypesByEnum.Add(enumValue, nodeType);
                }
            }
        }

        private bool IsActionGraphNodeType(CStructure nodeType)
        {
            foreach (string baseTypeName in nodeType.BaseTypes)
            {
                if (baseTypeName == "ActionGraphNode")
                    return true;

                CStructure? baseType = _lib.Types.GetValueOrDefault(baseTypeName) as CStructure;
                if (baseType != null && IsActionGraphNodeType(baseType))
                    return true;
            }
            return false;
        }

        public void Write010CaseStatements(StreamWriter writer)
        {
            foreach ((NodeType nodeTypeEnum, CType nodeType) in _nodeTypesByEnum)
            {
                CType argsType = _argTypesByNodeType[nodeType];
                writer.WriteLine(
                    $$"""
                    case NT_{{nodeTypeEnum}}:
                    {
                        {{argsType.Name.Replace("dtp::", "")}} args;
                        break;
                    }
                    """
                );
            }
        }

        public void Write010ArgTypes(StreamWriter writer)
        {
            BinaryTemplateWriter template = new(_lib, writer, _game);
            foreach (CType argsType in _argTypesByNodeType.Values)
            {
                _lib.CalculateAlignmentsAndSizes(argsType);
                template.WriteType(argsType);
            }
        }

        public void WriteCSharpFactory(TextWriter writer)
        {
            foreach ((NodeType nodeTypeEnum, CType nodeType) in _nodeTypesByEnum.OrderBy(p => (int)p.Key))
            {
                string nodeTypeName = NormalizeTypeName(nodeType.Name);
                writer.WriteLine($"{(int)nodeTypeEnum} => new {nodeTypeName}(),");
            }
        }

        public void WriteCSharpNodeTypes(string folderPath)
        {
            CSharpTypeWriter cs = new(_lib, new StringWriter(), _game);
            foreach (IGrouping<CType, NodeType> nodeEnumsOfType in _nodeTypesByEnum.GroupBy(p => p.Value, p => p.Key))
            {
                CType nodeType = nodeEnumsOfType.Key;
                string filePath = Path.Combine(folderPath, NormalizeTypeName(nodeType.Name) + ".cs");
                using var fileWriter = new StreamWriter(File.Create(filePath));
                cs.SetWriter(fileWriter);

                CType argType = _argTypesByNodeType[nodeType];
                string argTypeName = NormalizeTypeName(nodeType.Name) + "Properties";
                cs.AddTypeNameOverride(argType, argTypeName);

                _lib.CalculateAlignmentsAndSizes(argType);
                cs.WriteRootType(argType);

                WriteNodeClass(nodeEnumsOfType.First(), nodeType, argTypeName, new(fileWriter));
            }
        }

        private void WriteNodeClass(NodeType nodeTypeEnum, CType nodeType, string argsTypeName, IndentedTextWriter writer)
        {
            string nodeTypeEnumType = $"{_game}NodeType";
            string nodeTypeName = NormalizeTypeName(nodeType.Name);
            argsTypeName = argsTypeName.Replace("::", "._");
            writer.WriteLine(
                $$"""
                internal class {{nodeTypeName}} : Node<{{argsTypeName}}>
                {
                    public override int TypeId => {{(int)nodeTypeEnum}};

                """
            );
            writer.Indent++;

            (List<Slot?> inputSlots, List<Slot?> outputSlots) = GetSlots(nodeType);
            WriteNodeSlots("Inputs", inputSlots, writer);
            WriteNodeSlots("Outputs", outputSlots, writer);

            writer.Indent--;
            writer.WriteLine(
                """
                }
                """
            );
        }

        private (List<Slot?> InputSlots, List<Slot?> OutputSlots) GetSlots(CType nodeType)
        {
            JsonElement nodeElem = _slotsDoc.RootElement.GetProperty(nodeType.Name);
            List<Slot?> inputSlots = GetSlots(nodeElem.GetProperty("inputs"));
            List<Slot?> outputSlots = GetSlots(nodeElem.GetProperty("outputs"));
            return (inputSlots, outputSlots);
        }

        private static List<Slot?> GetSlots(JsonElement elem)
        {
            List<Slot?> slots = [];
            foreach (JsonProperty property in elem.EnumerateObject())
            {
                int slotIdx = int.Parse(property.Name);

                property.Value.TryGetProperty("name", out JsonElement nameElem);
                string? name = nameElem.ValueKind == JsonValueKind.String ? nameElem.GetString() : null;

                string type = property.Value.GetProperty("type").GetString()!.Replace("kAGIOType_", "");
                if (int.TryParse(type, out int typeInt))
                    type = ((SlotType)typeInt).ToString();

                while (slots.Count <= slotIdx)
                {
                    slots.Add(null);
                }
                slots[slotIdx] = new(name, type);
            }
            return slots;
        }

        private static void WriteNodeSlots(string propertyName, List<Slot?> slots, IndentedTextWriter writer)
        {
            if (slots.Count == 0)
                return;

            writer.WriteLine($"private static readonly Slot[] Static{propertyName} = [");
            writer.Indent++;

            for (int i = 0; i < slots.Count; i++)
            {
                Slot? slot = slots[i];
                if (slot != null)
                    writer.Write($"new(\"{slot.Name}\", SlotType.{slot.Type})");
                else
                    writer.Write("null");

                if (i < slots.Count - 1)
                    writer.Write(",");

                writer.WriteLine();
            }

            writer.Indent--;
            writer.WriteLine("];");
            writer.WriteLine($"public override Slot[] {propertyName} => Static{propertyName};");
            writer.WriteLine();
        }

        private static string NormalizeTypeName(string typeName)
        {
            typeName = Regex.Replace(typeName, @"^dtp::", "");

            if (typeName.StartsWith("AG"))
                typeName = typeName.Substring("AG".Length);

            if (typeName.StartsWith("Instance"))
                typeName = typeName.Substring("Instance".Length);

            typeName = typeName.Replace("_", "");
            //typeName = Regex.Replace(typeName, @"Vector(?=[A-Z]|$)", "Vec");
            //typeName = Regex.Replace(typeName, @"Evaluate(?=[A-Z]|$)", "Eval");
            //typeName = Regex.Replace(typeName, @"Camera(?=[A-Z]|$)", "Cam");
            //typeName = typeName.Replace("BattleConductor", "BC");
            typeName = Regex.Replace(typeName, @"Node$", "");
            typeName += "Node";
            if (Regex.IsMatch(typeName, @"^\d"))
                typeName = "_" + typeName;

            return typeName;
        }

        private enum SlotType
        {
            Undefined = 0x0,
            Any = 0x1,
            Signal = 0x2,
            Bool = 0x3,
            Int = 0x4,
            Float = 0x5,
            UInt32 = 0x6,
            Vector3 = 0x7,
            Position = 0x8,
            String = 0x9,
            UInt64 = 0xA,
            HashKey = 0xB,
            Size_t = 0xC
        }

        private enum NodeType
        {
            ActionGraphInstanceInputsNode = 0xA,
            ActionGraphInstanceOutputsNode = 0xB,
            AG_Coverpoint_Get_Index = 0x2EA,
            AG_Instance_IsValid = 0xDB,
            AG_Instance_MovesetProfile = 0x1FF,
            AG_Misc_InputSwitch = 0x133,
            AG_Misc_InputSwitch_ = 0x186,
            AG_Player_IsConcealed = 0x24D,
            AG_Player_MeleeMove = 0x24F,
            AG_Player_MeleeMove_GetCurrentTarget = 0x250,
            AG_Weapon_Ammo = 0x43,
            AGAddTraitNode = 0x10F,
            AGAddVecNode = 0x15E,
            AGArrayNode = 0x11E,
            AGBattleConductor_BroadcastNpcEventNode = 0x35,
            AGBattleConductor_BroadcastSoundEventNode = 0x30,
            AGBattleConductor_ChangeSettingsNode = 0x23,
            AGBattleConductor_DefZone = 0x37,
            AGBattleConductor_DespawnNPCs = 0x29,
            AGBattleConductor_ForceNpcAwarenessOfInstanceNode = 0x2A,
            AGBattleConductor_GetClosestEnemy = 0x2C,
            AGBattleConductor_GetGroupMembers = 0x2B,
            AGBattleConductor_GetNPCTypeCount = 0x2E,
            AGBattleConductor_GetNumIndividuals = 0x26,
            AGBattleConductor_GroupUseCover = 0x2F,
            AGBattleConductor_SetAttackCooldownNode = 0x31,
            AGBattleConductor_SetDynamicDifficulty = 0x2D,
            AGBattleConductor_SetMaxAssaultModeAttackersNode = 0x33,
            AGBattleConductor_Spawn = 0x25,
            AGBattleConductor_Status = 0x24,
            AGBattleConductor_SuppressionControl = 0x34,
            AGBattleConductor_WaypointSet = 0x36,
            AGBuffActivateNode = 0x77,
            AGCamera_ChangeOrResetStack = 0x10,
            AGCamera_ClimbSplineLogic = 0x1B,
            AGCamera_GetCurrentResourceID = 0x12,
            AGCamera_GetCurrentStack = 0x13,
            AGCamera_Is_Facing = 0x16,
            AGCamera_ReplaceResource = 0x1F,
            AGCamera_SetProperty = 0x22,
            AGCamera_Switch = 0x19,
            AGCamera_SwitchToSplineCam = 0x1A,
            AGCameraEvalCamSpline = 0x17,
            AGCameraEvalCamSplineBySpeed = 0x18,
            AGCameraGetActiveCameraIDNode = 0x1D,
            AGCameraGetPropertiesNode = 0x1E,
            AGCameraResetToDefaultsNode = 0x20,
            AGCameraSetZoomNode = 0x21,
            AGCameraShake = 0xE,
            AGCellNode = 0x2D5,
            AGCheckUnitLoadStatusNode = 0x2D2,
            AGCollectibleCameraInfo = 0x276,
            AGCollectibleRenderNode = 0x275,
            AGCollisionIDNode = 0xBF,
            AGCollisionRefNode = 0xC0,
            AGCollisionScaleNode = 0xC1,
            AGColorFromColorNode = 0x16F,
            AGColorToColorNode = 0x171,
            AGCoverPointNode = 0x2E8,
            AGCoverPointStatusNode = 0x2E9,
            AGCroftManorCompletedStory = 0x2F9,
            AGCroftManorQueryProfileGlobalProgress = 0x2FA,
            AGCrossProductNode = 0x15F,
            AGCurrentModeType = 0x9D,
            AGDarknessSIModeNode = 0x278,
            AGDBarkGetStatusNode = 0x2F7,
            AGDBarkOverrideNode = 0x2F8,
            AGDebugAssertNode = 0x7B,
            AGDebugDrawText = 0x79,
            AGDebugStartAfterLiveEditNode = 0x7E,
            AGDeformSnow = 0x282,
            AGDialogueActorAssignmentNode = 0x2F4,
            AGDialogueDialogueNode = 0x2F3,
            AGDialoguePlayLineNode = 0x2F5,
            AGDMiscAddSubtitleNode = 0x18D,
            AGDMiscGetSKUNode = 0x18E,
            AGDotProductNode = 0x160,
            AGDrawableAddModelToSlot = 0x6B,
            AGDrawableRemoveModelFromSlot = 0x6C,
            AGEnumSelectorNode = 0x187,
            AGEnumValueNode = 0x188,
            AGForceSwapDLCIcon = 0x198,
            AGFrameByFrameModeNode = 0x7C,
            AGFrameTriggerNode = 0x189,
            AGFrameTriggerNode_ = 0x18A,
            AGFreezeGame = 0x13B,
            AGFromVecNode = 0x168,
            AGGameRulesInstanceNode = 0x89,
            AGGameRulesMarkerNode = 0x8A,
            AGGameWorld_GetDifficultyNode = 0xA5,
            AGGameWorld_OnDifficultyChanged = 0xA6,
            AGGenChangeSettings = 0x3A,
            AGGenerator_Status = 0x3B,
            AGGetAspectRatio = 0x2F0,
            AGGetConditionStatus = 0x236,
            AGGetCoopPlayers = 0x239,
            AGGetInteraction = 0x235,
            AGGetNumberOfPlayers = 0x232,
            AGGetPlayer = 0x233,
            AGGetPlayerID = 0x234,
            AGGetPlayerInfo = 0x221,
            AGGetSurfaceStatus = 0x237,
            AGGetTargetUnderReticle = 0x3C,
            AGGetWeatherIntensityNode = 0x4D,
            AGGraphicsOverrideNode = 0x271,
            AGGraphicsOverridePercentNode = 0x272,
            AGHasTraitNode = 0x10E,
            AGHealthAndDamageNode = 0x95,
            AGHelicopterSetLookAtTarget = 0x1F6,
            AGHuntManager_GiveInstance = 0xA7,
            AGHuntManager_HowMany = 0xA8,
            AGHuntManager_ScareAndDisable = 0xA9,
            AGHuntManager_TakeInstance = 0xAA,
            AGInput_CheckRequestedInputAction = 0xAB,
            AGInput_StickRotateTracker = 0xB5,
            AGInputDisableActionNode = 0xAE,
            AGInputDisableInputActionTypesNode = 0xAD,
            AGInputGetActionNode = 0xAF,
            AGInstance_ActionGraphInstanceNode = 0xC,
            AGInstance_ForceProjectileCelebrationNode = 0x3E,
            AGInstance_GetOffset = 0xD0,
            AGInstance_PlacementNode = 0xE9,
            AGInstance_SpawnedPlacementNode = 0xEA,
            AGInstance1stLocalPlayerNode = 0x1FD,
            AGInstanceAlignToPoint = 0xB8,
            AGInstanceAllInRangeNode = 0xB9,
            AGInstanceAnimTimeScale = 0x115,
            AGInstanceBoneParamNode = 0xBB,
            AGInstanceCheckCharacter = 0xD3,
            AGInstanceCheckCharacterClass = 0xBD,
            AGInstanceCheckProximityToOtherInstanceNode = 0xBE,
            AGInstanceClothControl = 0xFC,
            AGInstanceComparePlacementIDs = 0xE8,
            AGInstanceCoordinatesNode = 0xED,
            AGInstanceDebugNameNode = 0x81,
            AGInstanceDestroy = 0xC6,
            AGInstanceDrawGroups = 0xC7,
            AGInstanceDynamicLightNode = 0xC8,
            AGInstanceDynamicMarkupComponentNode = 0xC3,
            AGInstanceDynamicMarkupOverrideNode = 0xC5,
            AGInstanceFadeOut = 0xC9,
            AGInstanceFlammableNode = 0xCA,
            AGInstanceFlammableProps = 0xCB,
            AGInstanceFollowTargetNode = 0x109,
            AGInstanceFXNode = 0xCD,
            AGInstanceGetAxis = 0xD1,
            AGInstanceGetBoneParam = 0xBC,
            AGInstanceGetPosNode = 0xCF,
            AGInstanceGiveBirth = 0xD6,
            AGInstanceHasAuthorityNode = 0x10D,
            AGInstanceHideUnhide = 0xD7,
            AGInstanceIsDynamicMarkupUsed = 0xDC,
            AGInstanceIsFacing = 0xDE,
            AGInstanceIsObjectID = 0xD9,
            AGInstanceIsPlayerID = 0xDA,
            AGInstanceIsVisible = 0xDF,
            AGInstanceKeepAlive = 0xE0,
            AGInstanceMaterialSwap = 0xE1,
            AGInstanceMaterialSwapCO = 0xE2,
            AGInstanceNetSendEventNode = 0xEF,
            AGInstanceNetSyncNode = 0xF0,
            AGInstanceNullPlacementIDNode = 0xE6,
            AGInstanceObjectNameNode = 0x82,
            AGInstanceOwnerPlayerNode = 0x1FE,
            AGInstancePaperdoll = 0xD8,
            AGInstancePhysicsAttachComponentNode = 0xC4,
            AGInstancePlacementIDNode = 0xE5,
            AGInstancePosNode = 0xF1,
            AGInstancePowerMeterEventsNode = 0xF2,
            AGInstanceQueryLookAtTarget = 0x10C,
            AGInstanceReset = 0xF4,
            AGInstanceResetCloth = 0xFE,
            AGInstanceSensibleNode = 0xF5,
            AGInstanceSetAnimGoalNode = 0xEB,
            AGInstanceSetDeactivationDistancesNode = 0x118,
            AGInstanceSetFlammableProps = 0xCC,
            AGInstanceSetGetPlacementIDNode = 0xE7,
            AGInstanceSetModel = 0xF9,
            AGInstanceSetNonuniformScale = 0x117,
            AGInstanceSetUniformScale = 0x116,
            AGInstanceSkeletonBindNode = 0x108,
            AGInstanceSwapMaterial = 0xE3,
            AGInstanceSwitchClothConfig = 0xFD,
            AGInstanceTeamCheck = 0xFF,
            AGInstanceTeleportNode = 0x100,
            AGInstanceTrackTarget = 0x101,
            AGInstanceTriggerNode = 0x102,
            AGInstanceWarpAttachNode = 0x106,
            AGInstanceWarpDetachNode = 0x107,
            AGInteractiveSequenceToggle = 0x200,
            AGInterpColorsNode = 0x159,
            AGInterpCurveResourceID = 0x15D,
            AGInterpFloatsNode = 0x15A,
            AGInterpIntsNode = 0x15B,
            AGInterpVectorsNode = 0x15C,
            AGInventoryNode = 0x222,
            AGIsInDefinitiveEdition = 0x197,
            AGIsInTrialMode = 0x196,
            AGIsOutfitRestrictedInPostGame = 0x199,
            AGJumpHelperNode = 0x2CF,
            AGLighting_LightGroupNode = 0x11F,
            AGLightingLightInstanceNode = 0x120,
            AGLiveSpawnerRelay = 0x1F4,
            AGLoadoutHasWeaponForItemInLoadoutNode = 0x11D,
            AGLoadUnitNode = 0x2D1,
            AGLocalToWorldNode = 0x161,
            AGLogicAnyNode = 0x122,
            AGLogicAnyNode_ = 0x123,
            AGLogicBlockerNode = 0x124,
            AGLogicBooleanOpMultiNode = 0x126,
            AGLogicBooleanOpNode = 0x125,
            AGLogicBooleanOpWithTriggerNode = 0x127,
            AGLogicConditionalRouteNode = 0x129,
            AGLogicConditionNode = 0x128,
            AGLogicHoldNode = 0x12A,
            AGLogicHoldUntilAllInputsNode = 0x12B,
            AGLogicHoldUntilAllInputsNode_ = 0x12C,
            AGLogicNOTNode = 0x12D,
            AGLogicOnceNode = 0x12E,
            AGLogicOnChangeNode = 0x12F,
            AGLogicOnStartedOnStoppedNode = 0x130,
            AGLogicRouteNode = 0x132,
            AGLogLogData = 0x83,
            AGLogPrintData = 0x84,
            AGLogPrintMultiData = 0x85,
            AGMagnitudeVecNode = 0x162,
            AGMathAbsNode = 0x13C,
            AGMathArithmeticNode = 0x13F,
            AGMathArithmeticNodeVer2 = 0x13E,
            AGMathAverageMinMax = 0x141,
            AGMathAverageWithPrevValuesNode = 0x140,
            AGMathBitOperation = 0x153,
            AGMathClampNode = 0x142,
            AGMathComparisonAngleNode = 0x144,
            AGMathComparisonInRangeNode = 0x147,
            AGMathComparisonNode = 0x146,
            AGMathComparisonNodeVer2 = 0x143,
            AGMathDirToRotation = 0x149,
            AGMathDrawParabola = 0x80,
            AGMathHysteresis = 0x148,
            AGMathIntComparisonNode = 0x145,
            AGMathPerlinNoise = 0x14A,
            AGMathRandomNumber = 0x14C,
            AGMathRandomSelect = 0x14D,
            AGMathRandomSequence = 0x14E,
            AGMathRotationToDir = 0x151,
            AGMathSetBitFlags = 0x152,
            AGMathSetIntNode = 0x155,
            AGMathSetNode = 0x154,
            AGMathSpringMassDamper = 0x158,
            AGMathSqrtNode = 0x13D,
            AGMathTrigNode = 0x156,
            AGMathUpDownCounter = 0x157,
            AGMessageReceiveCollisionNode = 0x16B,
            AGMessageReceiveNode = 0x169,
            AGMessageReceivePlayerNode = 0x16E,
            AGMessageReceivePushPullNode = 0x16D,
            AGMessageSendNode = 0x16A,
            AGMiscControllerNode = 0x88,
            AGMiscGoToMainMenu = 0x184,
            AGMiscPrevious = 0x176,
            AGMiscSetColorNode = 0x170,
            AGMiscSetColorNode_ = 0x17B,
            AGMiscTrialDemoPrompt = 0x2F2,
            AGMiscUTSurveyPrompt = 0x2F1,
            AGMultiplayerAdaptationNode = 0x92,
            AGMultiplayerLogPrintData = 0x86,
            AGNewGamePlusNode = 0x93,
            AGNormalizeVecNode = 0x163,
            AGNoticeableActionNode = 0x91,
            AGNPCAlertLevel = 0x19D,
            AGNPCAssaultMode = 0x19F,
            AGNPCAwareness = 0x1A0,
            AGNPCCallForHelp = 0x1A1,
            AGNPCCollisionReaction = 0x1D3,
            AGNPCCorpseManager = 0x1A2,
            AGNPCCrowNode = 0x1A3,
            AGNPCDespawn = 0x1A4,
            AGNPCEnableAction = 0x1A5,
            AGNPCEnableAltRangedAttacks = 0x1A7,
            AGNPCEnableGoal = 0x1A6,
            AGNPCEnableMeleeAttack = 0x1A8,
            AGNPCFollow = 0x1A9,
            AGNPCFollowCostraintNode = 0x1AA,
            AGNPCForceTargetForNextAttack = 0x1AB,
            AGNPCGesture = 0x1AC,
            AGNPCGetCurrentTarget = 0x1AD,
            AGNPCGetEventInfo = 0x19E,
            AGNPCGetWeapon = 0x1AE,
            AGNPCGroupID = 0x1C1,
            AGNPCHeldObject = 0x1AF,
            AGNPCIncapacitation = 0x1B0,
            AGNPCIsAware = 0x1B1,
            AGNPCIsGuarding = 0x1B2,
            AGNPCNeeds = 0x1B3,
            AGNPCPause = 0x1B4,
            AGNPCPlannerCheckGoal = 0x1B5,
            AGNPCPlannerObstacle = 0x1B6,
            AGNPCPuppetMode = 0x1B7,
            AGNPCSelectAltProjectile = 0x1B8,
            AGNPCSetAmbushMode = 0x1B9,
            AGNPCSetAssignedLocation = 0x1BA,
            AGNPCSetForcedCombatTarget = 0x1BB,
            AGNPCSetForcedFleeTarget = 0x1BC,
            AGNPCSetForcedInstanceAwareness = 0x1BD,
            AGNPCSetForcedMovementStyle = 0x1BE,
            AGNPCSetGoapSettings = 0x1C0,
            AGNPCSetGuardMode = 0x1C2,
            AGNPCSetLookAtTarget = 0x1C3,
            AGNPCSetMoverFlags = 0x1C4,
            AGNPCSetNextCoverPoint = 0x1C5,
            AGNPCSetPathSharingPenalty = 0x1C6,
            AGNPCSetPlanLayer = 0x1C7,
            AGNPCSetRepulsor = 0x1C8,
            AGNPCSetSenseCones = 0x1C9,
            AGNPCSetTargetOffset = 0x1BF,
            AGNPCSetWaypointSet = 0x1CB,
            AGNPCSetWeaponAndDutyStatus = 0x1CC,
            AGNPCStairHelper = 0x1D4,
            AGNPCStatus = 0x1CD,
            AGNPCTeam = 0x1CA,
            AGNPCTeamCheck = 0x1CE,
            AGNPCThreaten = 0x1CF,
            AGNPCTurretHoldFire = 0x1D0,
            AGNPCUpdateAccuracySettings = 0x1D1,
            AGNPCUseObject = 0x1D2,
            AGNullNode = 0x0,
            AGNullNode_ = 0x87,
            AGObjectAdvertiseEvent = 0x1D6,
            AGObjectAdvertiseProfileEnable = 0x1D7,
            AGObjectAdvertiseRequestStart = 0x1D8,
            AGObjectAdvertiseRequestStop = 0x1D9,
            AGObjectArmorNode = 0x1DA,
            AGObjectArmorTransitionNode = 0x1DB,
            AGObjectDamageNode = 0x1DC,
            AGObjectDestruct = 0x1DD,
            AGObjectDisableComponent = 0x1DE,
            AGObjectEnableComponent = 0x1DF,
            AGObjectEnableNPCInteraction = 0x1E0,
            AGObjectFluidContainerSetProperties = 0x1F3,
            AGObjectGetComponentProperties = 0x1E1,
            AGObjectHasComponent = 0x1E2,
            AGObjectHelicopterSetWaypoint = 0x1F5,
            AGObjectInputsNode = 0x1E3,
            AGObjectInstanceID = 0x1E4,
            AGObjectInteractCondition = 0x1ED,
            AGObjectInteractEvent = 0x1EB,
            AGObjectInteractForceAbort = 0x1E8,
            AGObjectInteractForceComplete = 0x1E7,
            AGObjectInteractForceEnd = 0x1E6,
            AGObjectInteractForceStart = 0x1E5,
            AGObjectInteractGlobalUI = 0x1EC,
            AGObjectInteractProfileEnable = 0x1E9,
            AGObjectInteractPushPull = 0x1EA,
            AGObjectInteractSuppressUI = 0x1EE,
            AGObjectNetDataGetNode = 0x19B,
            AGObjectNetDataOnChangeNode = 0x19C,
            AGObjectNetDataSetNode = 0x19A,
            AGObjectNode = 0x1D5,
            AGObjectOutputsNode = 0x1EF,
            AGObjectProgressionStateRefresh = 0x78,
            AGObjectShardSpawnedNode = 0x1F0,
            AGObjectTransitionNode = 0x1F1,
            AGObjectUnitID = 0x1F2,
            AGOccluderNode = 0x2D7,
            AGOutfitPieceEquippedNode = 0x245,
            AGOverrideConditionStatus = 0x238,
            AGOverrideShadowSplitsNode = 0x274,
            AGPACGenerateSpawnList = 0x38,
            AGPACSetTier = 0x39,
            AGPCBenchmarkNode = 0x2EC,
            AGPerformanceSetParamsNode = 0x201,
            AGPhysicsApplyPhysicsNode = 0x202,
            AGPhysicsCollisionGroupOverride = 0x203,
            AGPhysicsGetForceOnBodyNode = 0x204,
            AGPhysicsGetWindNode = 0x205,
            AGPhysicsGlobalJoints = 0x206,
            AGPhysicsGravityNode = 0x207,
            AGPhysicsJointCreate = 0x20F,
            AGPhysicsJointDestroy = 0x210,
            AGPhysicsJointLimit = 0x211,
            AGPhysicsJointLink = 0x212,
            AGPhysicsJointMotor = 0x213,
            AGPhysicsJointRange = 0x214,
            AGPhysicsJointRopeLength = 0x215,
            AGPhysicsJointRopeVisibility = 0x216,
            AGPhysicsJointState = 0x217,
            AGPhysicsLineProbeNode = 0x208,
            AGPhysicsMoveToNode = 0x209,
            AGPhysicsRagdollJointDataNode = 0x20B,
            AGPhysicsRBProperties = 0x20A,
            AGPhysicsSpatialAwarenessProbeNode = 0x20C,
            AGPhysicsTargetJointMotor = 0x218,
            AGPhysicsTimeQuake = 0x20D,
            AGPhysicsVelocityOverride = 0x20E,
            AGPlatformNode = 0x2ED,
            AGPlayer_CombatStatus_GetStealthLoopState = 0x255,
            AGPlayer_CombatStatus_Override = 0x254,
            AGPlayer_DBND_Indicator = 0x252,
            AGPlayer_InteractChangeType = 0x23B,
            AGPlayer_RappelRopeControl = 0x256,
            AGPlayer_SetCombatEncounterState = 0x251,
            AGPlayerAmmoMenuNode = 0x230,
            AGPlayerAmmoNode = 0x225,
            AGPlayerAnimGraphSetCustomMovesetHost = 0x21A,
            AGPlayerAnimGraphSetDialogueOverlayHost = 0x21B,
            AGPlayerAnimGraphSetGHOST = 0x219,
            AGPlayerAttachToMarkup = 0x21C,
            AGPlayerCultureKnowledgeNode = 0x248,
            AGPlayerCurrentCostumeZoneID = 0x258,
            AGPlayerDefaultCostumeZoneID = 0x257,
            AGPlayerDetachFromMarkup = 0x21D,
            AGPlayerEquipOutfitPieceNode = 0x243,
            AGPlayerForceSenseTargetNode = 0x22A,
            AGPlayerFreeCrafting = 0x253,
            AGPlayerGetAttachedMarkup = 0x21E,
            AGPlayerGetCollectibleNode = 0x242,
            AGPlayerGetLootQuantityNode = 0x23F,
            AGPlayerGetProxyInfoNode = 0x22B,
            AGPlayerGetSEMStatsNode = 0x24B,
            AGPlayerGetSenseTargetNode = 0x229,
            AGPlayerGetTeamID = 0x23A,
            AGPlayerGiveCollectibleNode = 0x240,
            AGPlayerGiveLootNode = 0x23E,
            AGPlayerHasCollectibleNode = 0x241,
            AGPlayerHasContentUnlockedNode = 0x247,
            AGPlayerHostedOverlayToggle = 0x224,
            AGPlayerInstinctModeNode = 0x22D,
            AGPlayerOnSetAnimGoalNode = 0x22F,
            AGPlayerRepairBrokenOutfitPieceNode = 0x244,
            AGPlayerRespawnNode = 0x226,
            AGPlayerSeizeNode = 0x223,
            AGPlayerSEMStatsNode = 0x24C,
            AGPlayerSetHeadTrackLookAtTargetNode = 0x23D,
            AGPlayerSetJumpTarget = 0x21F,
            AGPlayerSetLookAtTargetNode = 0x23C,
            AGPlayerStatNotifications = 0x24E,
            AGPlayerSteering = 0x220,
            AGPlayerTeam = 0x249,
            AGPlayerTriggerNode = 0xEC,
            AGPlayerUnlockContentNode = 0x246,
            AGPlayMovie = 0x175,
            AGPlaystylePathNode = 0x112,
            AGPortalNode = 0x2D6,
            AGProgressionRank = 0x9C,
            AGQueryActiveTimeOfDayNode = 0x48,
            AGQueryActiveWeatherNode = 0x44,
            AGQueryVOImmersionModeNode = 0x2F6,
            AGRagdollGetPosNode = 0x259,
            AGRagdollSetParamsNode = 0x25A,
            AGRagdollWeldRootNode = 0x25B,
            AGRandomChanceNode = 0x14B,
            AGRandomTriggerNode = 0x150,
            AGReduceResolutionNode = 0x27F,
            AGReleaseGraphicsOverrideNode = 0x273,
            AGRemoveTraitNode = 0x110,
            AGRevealSkillNode = 0x111,
            AGRewards_ActivateOutcast = 0x25C,
            AGRewards_RevealCollectibleOnMap = 0x25D,
            AGRopeVisualOnly = 0x1F7,
            AGRopeVisualOnlyV2 = 0x1F8,
            AGRopeWithCollision = 0x1F9,
            AGRotateCollectible = 0x277,
            AGScaleVecNode = 0x164,
            AGScriptableNode = 0x266,
            AGScriptCallFunctionNode = 0x265,
            AGScriptGetVarNode = 0x263,
            AGScriptSetVarNode = 0x264,
            AGSelectorNode = 0x121,
            AGSensible_SetProfile = 0x6D,
            AGSetActiveTimeOfDayNode = 0x49,
            AGSetActiveWeatherNode = 0x45,
            AGSetFreezeOnCameraCut = 0x288,
            AGSetHairCollisionPlaneNode = 0x27D,
            AGSetHairLODProfileOverrideNode = 0x27A,
            AGSetHairObjectNode = 0x27B,
            AGSetHairRenderProfileNode = 0x279,
            AGSetHairSimulationProfileNode = 0x27C,
            AGSetMacroPermutationDataNode = 0x2E3,
            AGSetMotionBlur = 0x283,
            AGSetString = 0x192,
            AGSetVecNode = 0x165,
            AGSetWeatherIntensityNode = 0x4C,
            AGSetWetness = 0x27E,
            AGSoundConversationNode = 0x26C,
            AGSoundMicrophoneNode = 0x26D,
            AGSoundVocalNode = 0x26F,
            AGSpawn = 0x11B,
            AGSpawnObject = 0x119,
            AGSpawnObjectSelectable = 0x11A,
            AGSpawnPointNode = 0x2EB,
            AGSpawnSquib = 0x11C,
            AGStartNode = 0x17C,
            AGStationIsClientNode = 0x28B,
            AGStationIsHostNode = 0x28A,
            AGStationIsStandaloneNode = 0x28C,
            AGStringCompare = 0x190,
            AGStringConcatenate = 0x191,
            AGSubGraphNode = 0x28D,
            AGSubtractVecNode = 0x166,
            AGSwitchSelect = 0x134,
            AGSystemicMissionStringGenerator = 0x194,
            AGTime_GetSystemDateAndTime = 0x139,
            AGTimeDelayNode = 0x135,
            AGTimeFrameDelayNode = 0x136,
            AGTimeMult = 0x2EE,
            AGTimeOfDayEventsNode = 0x4A,
            AGTimeOfDayInfoNode = 0x4B,
            AGTimePersistentPlaytimeStopwatch = 0x138,
            AGTimeQuakeManagerNode = 0x13A,
            AGTimer = 0x2EF,
            AGTimeStopwatch = 0x137,
            AGToggleEarTransmissionNode = 0x281,
            AGToggleSubSurfaceScatteringNode = 0x280,
            AGToVecNode = 0x167,
            AGTriggerMasterNode = 0x103,
            AGTriggerNextFrameNode = 0x180,
            AGTwitchBroadcastListenerNode = 0x6A,
            AGTwitchKeywordListenerNode = 0x68,
            AGTwitchTriggerGiftEventNode = 0x69,
            AGUnitDebugNameNode = 0x2D3,
            AGUnitIDNode = 0x2D8,
            AGUnitInstanceIDNode = 0x104,
            AGUnitJumpHelperNode = 0x2CE,
            AGUnitSetAmbientLightNode = 0x2D4,
            AGUnitWaitForStreamingNode = 0x2DA,
            AGUnloadUnitNode = 0x2D9,
            AGValidInstance = 0x2E4,
            AGValidPhysicsJoint = 0x2E5,
            AGValueConvertorNode = 0x18B,
            AGWater_Distortion = 0x76,
            AGWaterVolume_Currents = 0x72,
            AGWaterVolume_Detection = 0x73,
            AGWaterVolume_GetWaterLevelForInstance = 0x74,
            AGWaterVolume_GetWaterLevelForPosition = 0x75,
            AGWaterVolume_Settings = 0x71,
            AGWeatherEventsNode = 0x46,
            AGWeatherInfoNode = 0x47,
            AGWeatherIntensityChangesNode = 0x4E,
            AGWwiseAmbient = 0x8,
            AGWwiseEvent = 0x1,
            AGWwisePortalObstruction = 0x4,
            AGWwisePortalOcclusion = 0x5,
            AGWwiseRTPC = 0x6,
            AGWwiseSkeletalComponentNode = 0x26E,
            AGWwiseSpeaker = 0x7,
            AGWwiseState = 0x2,
            AGWwiseSwitch = 0x3,
            AGZoneID = 0x2CA,
            AGZoneMarker = 0x2E1,
            Camera_ResourceID = 0xD,
            CameraEnableCategory = 0x11,
            CameraGetScreenPos = 0x14,
            CameraPaperdollPrediction = 0x15,
            CameraSplineNode = 0xF,
            Campsite_Tutorials = 0x290,
            CampsiteDataNode = 0x298,
            CampsiteEvents = 0x28E,
            CampsiteOpen = 0x28F,
            ChallengeTombDisplayStatusIcon = 0x2C1,
            ChallengeTombPrepareLevelEndNode = 0x2BD,
            ChallengeTombTimerNode = 0x2BC,
            ChallengeTombTriggeredEmoteNode = 0x2C0,
            ChallengeTombTriggerLevelEndNode = 0x2BE,
            ChallengeTombTriggerTombConfirmationNode = 0x2BF,
            CheckAnyButtonPressed = 0xAC,
            CheckModeState = 0x2C9,
            CompleteMarkerObject = 0x2A3,
            CustomizationScreenNode = 0x2C4,
            Debug_SetDebugNpc = 0x7D,
            DebugDrawVector = 0x7A,
            DeviceRouter = 0xB3,
            DisableAdrenaline = 0x10B,
            DLCLicenseCheck = 0x2CC,
            DLCLicenseToggle = 0x2CD,
            EnableAdrenaline = 0x10A,
            Experience_Adrenaline = 0x99,
            Experience_ToggleAutoXpAward = 0x9B,
            ExperienceAdjustMaxHealth = 0x9A,
            FireWeaponNode = 0x40,
            FXATimelinePlayNode = 0xCE,
            FxModifierNode = 0x267,
            FxWindPointNode = 0x26B,
            FxWindSplineModifier = 0x26A,
            FxWindUnit = 0x269,
            FxWindUnitEnabler = 0x268,
            GameObjectiveMenuOverrideNode = 0x8C,
            GameObjectiveNagNode = 0x8E,
            GameObjectiveNode = 0x8B,
            GameTutorialStep = 0x90,
            GameWorldPlayerClassTotalsNode = 0xA4,
            GameWorldQuerySecurityLevelNode = 0xA1,
            GameWorldSecuritySystemNode = 0xA3,
            GameWorldSetTensionNode = 0xA2,
            GenerateUniqueIDNode = 0x173,
            GenericLiveNode = 0x309,
            GetHeadingToOtherInstanceNode = 0xD2,
            GetLocalString = 0x193,
            GetMarkerRatio = 0x29A,
            GetMovieID = 0x29B,
            GetTargetScreenPos = 0xD4,
            GiveExperienceAward = 0x96,
            GiveSkillPoints = 0x98,
            GiveXP = 0x97,
            GlobalStreamLayerNode = 0x2E2,
            ImageStreamNode = 0x29F,
            Indicator = 0x2A1,
            Input_IsInputActionEnabled = 0xB0,
            Instance_ActivateReticule = 0x114,
            Instance_DamageData = 0xEE,
            Instance_GetClosestBone = 0xD5,
            Instance_HealthState = 0x94,
            Instance_ProjectileOwnerNotify = 0xF3,
            Instance_StateControlCondition = 0xFA,
            Instance_StateControlProperty = 0xFB,
            InstanceAnimGoalChanged = 0xBA,
            InstanceContainerNode = 0xC2,
            InstanceIsAnimGoal = 0xDD,
            InstanceMarkerPos = 0x113,
            InstanceWaitUntilAlive = 0x105,
            Interact_Set = 0x1FA,
            InventoryGetOwnerNode = 0x1FB,
            InventoryGetOwnerNode_ = 0x1FC,
            IsChallengeTombCompletedOnceNode = 0x2C7,
            IsInChallengeTombModeNode = 0x2C5,
            LevelObjectiveNode = 0x8F,
            Logic_PassThrough = 0x131,
            Math_RandomStickPos = 0x14F,
            MeshPlacementNode = 0x2E7,
            Message_ReceiveDamage = 0x16C,
            Misc_MapRevealUnit = 0x17E,
            Misc_PadShock = 0xB7,
            Misc_RegisterExtraFingerOfGod = 0x17D,
            MiscClearNamedValue = 0x172,
            MiscGetNamedValue = 0x18C,
            MiscInputAction = 0xB6,
            MiscIsInFastTravelNode = 0x18F,
            MiscPatchCheckpointSave = 0x183,
            MiscPersistentDataNode = 0x174,
            MiscQueryGlobalProgressNode = 0x50,
            MiscRegionManagerNode = 0x177,
            MiscSetGlobalProgressNode = 0x4F,
            MiscSetInputActionTracking = 0x179,
            MiscSetNamedValue = 0x17A,
            MiscTriggerGroupNode = 0x17F,
            MiscTriggerStreamSignalNode = 0x181,
            MiscTriggerStreamStatesNode = 0x182,
            Movie_PlayBinkMovieNode = 0x291,
            Movie_PlayScaleformMovieNode = 0x292,
            Movie_SlaveMovieNode = 0x293,
            Movie_StopMovieNode = 0x294,
            Movie_UnSlaveMovieNode = 0x295,
            ObjectiveProgressionNode = 0x8D,
            PhotoMode_SetupFrameFilter = 0x289,
            Player_DeathOptions = 0x22E,
            Player_SnapToCachedTarget = 0x228,
            Player_SnapToRangedSenseTarget = 0x227,
            Player_UnitChecksNode = 0x22C,
            PlayerProcessSwingNode = 0x231,
            Projectile_GetInfo = 0x3F,
            Projectile_SetStateNode = 0x3D,
            RegionNotificationSuppression = 0x2A9,
            RelocateNode = 0x178,
            Render_OverrideDecalVolume = 0x286,
            Render_OverrideFogVolume = 0x285,
            Render_OverrideSnowDeformationComponent = 0x284,
            Render_OverrideWetnessVolume = 0x287,
            RevealMarkerObject = 0x2A2,
            Rewards_CollectibleAGHelper = 0x260,
            RewardsChooseCollectibleNode = 0x25E,
            RewardsCollectibleUIMessengerNode = 0x25F,
            Scaleform_ImageResourceID = 0x29D,
            Scaleform_ImageStreamLoadIntoMovie = 0x29E,
            Scaleform_ImageStreamPreloadResource = 0x2A0,
            Scaleform_UILayer = 0x2AC,
            ScaleformAccessibilityOptionsNode = 0x2A7,
            ScaleformActionPromptNode = 0x296,
            ScaleformCameraAttachNode = 0x297,
            ScaleformFunctionCallNode = 0x299,
            ScaleformHUDNode = 0x29C,
            ScaleformISSMovieNode = 0x2A8,
            ScaleformLocalSpaceAttachNode = 0x2A4,
            ScaleformMarkerAttachNode = 0x2A5,
            ScaleformMovieNode = 0x2A6,
            ScaleformSetVariableNode = 0x2AA,
            ScaleformTextPrompt = 0x2AB,
            ScaleformYesNoNotification = 0x2B0,
            ScoreAttackEventsNode = 0x2C6,
            SendMetricsNode = 0x195,
            SetAction = 0xB1,
            SetMaterialParams = 0xF6,
            SetMaterialTextureParam = 0xF7,
            SetVCacheParams = 0xF8,
            Shake_ResourceID = 0x1C,
            StartGameplayNode = 0x2C3,
            StickToWorld = 0xB2,
            StickZone = 0xB4,
            SurvivalInstinctNode = 0x9F,
            SurvivalInstinctSurveyNode = 0xA0,
            Tomb_DataNode = 0x2B1,
            Ui_BlockStatusScreen = 0x2B2,
            UI_DisplayButton = 0x2B3,
            UI_DisplayNotification = 0x2B4,
            UI_DisplayObjectiveListNotification = 0x2C2,
            UI_DisplayRotationButton = 0x2B5,
            UI_SocialEvents = 0x2BA,
            UICollectionLoadOrUnload = 0x2BB,
            UiIsCurrentMenuStateNode = 0x2B6,
            UiMenuStateNode = 0x2B7,
            UiMenuStateSetNode = 0x2B8,
            UIPlusDangerPrompt = 0x2AD,
            UIQuickTimeEvent = 0x2B9,
            UnitCustomizationManagerActivateNode = 0x2DE,
            UnitCustomizationManagerDeactivateNode = 0x2DF,
            UnitCustomizationManagerGetInfoNode = 0x2E0,
            UnitFireTimesOfDayStreamingNode = 0x2DC,
            UnitFireWorldStateNode = 0x2DB,
            UnitMarkupNode = 0x2E6,
            UnitStreamLayerNode = 0x2DD,
            UnlockChallengeTombNode = 0x2C8,
            VR_Calibrate = 0x2FC,
            VR_CameraMode_Get = 0x2FD,
            VR_CameraMode_OnChange = 0x2FF,
            VR_CameraMode_Set = 0x2FE,
            VR_GetControllerInfo = 0x301,
            VR_GetHmdInfo = 0x300,
            VR_GetParabola = 0x306,
            VR_GetSnapMovement = 0x307,
            VR_Globals = 0x302,
            VR_PlaystateNode = 0x2FB,
            VR_SetHmdRelativeMovement = 0x304,
            VR_SetParabola = 0x305,
            VR_SetPlayArea = 0x308,
            VR_Settings = 0x303,
            WaveShakeNode = 0x185,
            WeaponChangeLoadoutNode = 0x41,
            WeaponQueryLoadoutNode = 0x42,
            WidgetGroupHideShow = 0x2AE,
            WidgetGroupIsVisible = 0x2AF
        }
    }
}
