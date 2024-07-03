﻿using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using Fika.Core.Coop.Players;
using Fika.Core.Networking.Packets;
using System;
using System.Collections.Generic;

namespace Fika.Core.Coop.ClientClasses
{
    public sealed class CoopClientSharedQuestController(Profile profile, InventoryControllerClass inventoryController,
        IQuestActions session, CoopPlayer player, bool fromServer = true) : LocalQuestControllerClass(profile, inventoryController, session, fromServer)
    {
        private readonly CoopPlayer player = player;
        private readonly List<string> lastFromNetwork = [];
        private readonly HashSet<string> acceptedTypes = [];
        private readonly HashSet<string> lootedTemplateIds = [];

        public override void Init()
        {
            base.Init();
            foreach (FikaPlugin.EQuestSharingTypes shareType in (FikaPlugin.EQuestSharingTypes[])Enum.GetValues(typeof(FikaPlugin.EQuestSharingTypes)))
            {
                if (FikaPlugin.QuestTypesToShareAndReceive.Value.HasFlag(shareType))
                {
                    switch (shareType)
                    {
                        case FikaPlugin.EQuestSharingTypes.Kill:
                            acceptedTypes.Add("Elimination");
                            acceptedTypes.Add(shareType.ToString());
                            break;
                        case FikaPlugin.EQuestSharingTypes.Item:
                            acceptedTypes.Add("FindItem");
                            break;
                        case FikaPlugin.EQuestSharingTypes.Location:
                            acceptedTypes.Add("Exploration");
                            acceptedTypes.Add("Discover");
                            acceptedTypes.Add("VisitPlace");
                            acceptedTypes.Add(shareType.ToString());
                            break;
                        case FikaPlugin.EQuestSharingTypes.PlaceBeacon:
                            acceptedTypes.Add(shareType.ToString());
                            break;
                    }
                }
            }
        }

        public override void OnConditionValueChanged(IConditionCounter conditional, EQuestStatus status, Condition condition, bool notify = true)
        {
            base.OnConditionValueChanged(conditional, status, condition, notify);
            if (lastFromNetwork.Contains(condition.id))
            {
                lastFromNetwork.Remove(condition.id);
                return;
            }
            SendQuestPacket(conditional, condition);
        }

        public void AddNetworkId(string id)
        {
            if (!lastFromNetwork.Contains(id))
            {
                lastFromNetwork.Add(id);
            }
        }

        public void AddLootedTemplateId(string templateId)
        {
            if (!lootedTemplateIds.Contains(templateId))
            {
                lootedTemplateIds.Add(templateId);
            }
        }

        public bool CheckForTemplateId(string templateId)
        {
            return lootedTemplateIds.Contains(templateId);
        }

        private void SendQuestPacket(IConditionCounter conditional, Condition condition)
        {
            if (conditional is GClass1260 quest)
            {
                TaskConditionCounterClass counter = quest.ConditionCountersManager.GetCounter(condition.id);
                if (counter != null)
                {
                    if (!ValidateQuestType(counter))
                    {
                        return;
                    }

                    QuestConditionPacket packet = new(player.Profile.Info.MainProfileNickname, counter.Id, counter.SourceId);
#if DEBUG
                    FikaPlugin.Instance.FikaLogger.LogInfo("SendQuestPacket: Sending quest progress");
#endif
                    player.PacketSender.SendQuestPacket(ref packet);
                }
            }
        }

        internal void ReceiveQuestPacket(ref QuestConditionPacket packet)
        {
            AddNetworkId(packet.Id);
            foreach (QuestClass quest in Quests)
            {
                if (quest.Id == packet.SourceId && quest.QuestStatus == EQuestStatus.Started)
                {
                    TaskConditionCounterClass counter = quest.ConditionCountersManager.GetCounter(packet.Id);
                    if (counter != null)
                    {
                        if (!ValidateQuestType(counter))
                        {
                            return;
                        }

                        counter.Value++;
                        NotificationManagerClass.DisplayMessageNotification($"Received shared quest progression from {packet.Nickname}",
                            iconType: EFT.Communications.ENotificationIconType.Quest);
                    }
                }
            }
        }

        internal void ReceiveQuestItemPacket(ref QuestItemPacket packet)
        {
            if (!string.IsNullOrEmpty(packet.ItemId))
            {
                Item item = player.FindItem(packet.ItemId, true);
                if (item != null)
                {
                    InventoryControllerClass playerInventory = player.InventoryControllerClass;
                    GStruct414<GInterface339> pickupResult = InteractionsHandlerClass.QuickFindAppropriatePlace(item, playerInventory,
                        playerInventory.Inventory.Equipment.ToEnumerable(),
                        InteractionsHandlerClass.EMoveItemOrder.PickUp, true);

                    if (pickupResult.Succeeded && playerInventory.CanExecute(pickupResult.Value))
                    {
                        AddLootedTemplateId(item.TemplateId);
                        playerInventory.RunNetworkTransaction(pickupResult.Value);
                        NotificationManagerClass.DisplayMessageNotification($"{packet.Nickname} picked up {item.Name.Localized()}",
                            iconType: EFT.Communications.ENotificationIconType.Quest);
                    }
                }
            }
        }

        /// <summary>
        /// Validates quest typing, some quests use CounterCreator which we also need to validate.
        /// </summary>
        /// <param name="counter">The counter to validate</param>
        /// <returns>Returns true if the quest type is valid, returns false if not</returns>
        internal bool ValidateQuestType(TaskConditionCounterClass counter)
        {
            if (acceptedTypes.Contains(counter.Type))
            {
                return true;
            }

            if (counter.Type == "CounterCreator")
            {
                ConditionCounterCreator CounterCreator = (ConditionCounterCreator)counter.Template;

#if DEBUG
                FikaPlugin.Instance.FikaLogger.LogInfo($"CoopClientSharedQuestController:: ValidateQuestType: CounterCreator Type {CounterCreator.type}");
#endif

                if (acceptedTypes.Contains(CounterCreator.type.ToString()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
