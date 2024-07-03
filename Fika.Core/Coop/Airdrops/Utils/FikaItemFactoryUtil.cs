﻿using BepInEx.Logging;
using Comfort.Common;
using Coop.Airdrops;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Fika.Core.Coop.Airdrops.Models;
using Newtonsoft.Json;
using SPT.Common.Http;
using System.Linq;

namespace Fika.Core.Coop.Airdrops.Utils
{
    /// <summary>
    /// Originally developed by SPT <see href="https://dev.sp-tarkov.com/SPT/Modules/src/branch/master/project/SPT.Custom/Airdrops/Utils/ItemFactoryUtil.cs"/>
    /// </summary>
    public class FikaItemFactoryUtil
    {
        private readonly ItemFactory itemFactory;
        ManualLogSource logSource;

        public FikaItemFactoryUtil()
        {
            itemFactory = Singleton<ItemFactory>.Instance;
            logSource = Logger.CreateLogSource("ItemFactoryUtil");
        }

        public void BuildContainer(LootableContainer container, FikaAirdropConfigModel config, string dropType)
        {
            string containerId = config.ContainerIds[dropType];
            if (itemFactory.ItemTemplates.TryGetValue(containerId, out ItemTemplate template))
            {
                Item item = itemFactory.CreateItem(containerId, template._id, null);
                item.Id = Singleton<GameWorld>.Instance.MainPlayer.InventoryControllerClass.NextId;
                LootItem.CreateLootContainer(container, item, "CRATE", Singleton<GameWorld>.Instance);
            }
            else
            {
                logSource.LogError($"[SPT-AIRDROPS]: unable to find template: {containerId}");
            }
        }

        public void BuildClientContainer(LootableContainer container, Item rootItem)
        {
            LootItem.CreateLootContainer(container, rootItem, "CRATE", Singleton<GameWorld>.Instance);
        }

        public async void AddLoot(LootableContainer container, FikaAirdropLootResultModel lootToAdd)
        {
            FikaPlugin.Instance.FikaLogger.LogInfo($"ItemFactory::AirDropLoot: Reading loot from server. LootToAdd: {lootToAdd.Loot.Count()}");

            Item actualItem;
            foreach (FikaAirdropLootModel item in lootToAdd.Loot)
            {
                ResourceKey[] resources;
                if (item.IsPreset)
                {
                    actualItem = itemFactory.GetPresetItem(item.Tpl);
                    actualItem.SpawnedInSession = true;
                    actualItem.GetAllItems().ExecuteForEach(x => x.SpawnedInSession = true);
                    resources = actualItem.GetAllItems().Select(x => x.Template).SelectMany(x => x.AllResources).ToArray();
                }
                else
                {
                    actualItem = itemFactory.CreateItem(item.ID, item.Tpl, null);
                    actualItem.StackObjectsCount = item.StackCount;
                    actualItem.SpawnedInSession = true;

                    resources = actualItem.Template.AllResources.ToArray();
                }

                container.ItemOwner.MainStorage[0].Add(actualItem);
                await Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(PoolManager.PoolsCategory.Raid, PoolManager.AssemblyType.Local, resources, JobPriority.Immediate, null, PoolManager.DefaultCancellationToken);
            }

            if (Singleton<FikaAirdropsManager>.Instance != null)
            {
                Singleton<FikaAirdropsManager>.Instance.ClientLootBuilt = true;
            }
        }

        public FikaAirdropLootResultModel GetLoot()
        {
            string json = RequestHandler.GetJson("/client/location/getAirdropLoot");
            FikaAirdropLootResultModel result = JsonConvert.DeserializeObject<FikaAirdropLootResultModel>(json);

            return result;
        }
    }
}