using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class NpcTradeStockData
    {
        [SerializeField] private string productId;
        [SerializeField] private int remaining;
        [SerializeField] private bool acquired;

        public string ProductId { get => productId; set => productId = value; }
        public int Remaining { get => remaining; set => remaining = Mathf.Max(0, value); }
        public bool Acquired { get => acquired; set => acquired = value; }
    }

    [Serializable]
    public sealed class NpcTradeStateData
    {
        [SerializeField] private string npcId;
        [SerializeField] private int day;
        [SerializeField] private int availableFunds;
        [SerializeField] private List<NpcTradeStockData> stock = new();

        public string NpcId { get => npcId; set => npcId = value; }
        public int Day { get => day; set => day = value; }
        public int AvailableFunds { get => availableFunds; set => availableFunds = Mathf.Max(0, value); }
        public List<NpcTradeStockData> Stock => stock ??= new List<NpcTradeStockData>();
    }

    public static class NpcTradeLedger
    {
        public static NpcTradeStateData GetOrRefresh(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds)
        {
            if (sceneData == null)
                throw new ArgumentNullException(nameof(sceneData));
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("NPC id cannot be empty.", nameof(npcId));

            sceneData.NpcTrades ??= new List<NpcTradeStateData>();
            var state = sceneData.NpcTrades.FirstOrDefault(item => item.NpcId == npcId);
            if (state == null)
            {
                state = new NpcTradeStateData
                {
                    NpcId = npcId,
                    Day = day,
                    AvailableFunds = startingFunds
                };
                sceneData.NpcTrades.Add(state);
            }
            else if (state.Day != day)
            {
                state.Day = day;
                state.AvailableFunds = startingFunds;
            }

            return state;
        }

        public static bool TrySpendFunds(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            int amount)
        {
            if (amount < 0)
                return false;

            var state = GetOrRefresh(sceneData, npcId, day, startingFunds);
            if (state.AvailableFunds < amount)
                return false;

            state.AvailableFunds -= amount;
            return true;
        }

        public static void AddFunds(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            int amount)
        {
            if (amount <= 0)
                return;

            GetOrRefresh(sceneData, npcId, day, startingFunds).AvailableFunds += amount;
        }

        public static void AddAcquiredStock(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            string productId,
            int amount)
        {
            if (string.IsNullOrWhiteSpace(productId) || amount <= 0)
                return;

            var state = GetOrRefresh(sceneData, npcId, day, startingFunds);
            var entry = state.Stock.FirstOrDefault(item =>
                item.Acquired && item.ProductId == productId);
            if (entry == null)
            {
                entry = new NpcTradeStockData
                {
                    ProductId = productId,
                    Acquired = true
                };
                state.Stock.Add(entry);
            }

            entry.Remaining += amount;
        }

        public static int GetAcquiredStock(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            string productId)
        {
            var state = GetOrRefresh(sceneData, npcId, day, startingFunds);
            return state.Stock.FirstOrDefault(item =>
                item.Acquired && item.ProductId == productId)?.Remaining ?? 0;
        }

        public static IReadOnlyList<NpcTradeStockData> GetAllAcquiredStock(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds)
        {
            return GetOrRefresh(sceneData, npcId, day, startingFunds)
                .Stock
                .Where(item => item != null &&
                    item.Acquired &&
                    item.Remaining > 0)
                .ToList();
        }

        public static bool TryConsumeAcquiredStock(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            string productId)
        {
            var state = GetOrRefresh(sceneData, npcId, day, startingFunds);
            NpcTradeStockData entry = state.Stock.FirstOrDefault(item =>
                item.Acquired &&
                item.ProductId == productId &&
                item.Remaining > 0);
            if (entry == null)
                return false;

            entry.Remaining--;
            return true;
        }

        public static void RestoreAcquiredStock(
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            string productId)
        {
            AddAcquiredStock(
                sceneData,
                npcId,
                day,
                startingFunds,
                productId,
                1);
        }
    }
}
