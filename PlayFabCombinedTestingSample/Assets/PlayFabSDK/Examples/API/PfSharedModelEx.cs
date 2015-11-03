using System;
using System.Collections.Generic;

namespace PlayFab.Examples
{
    public static class PfSharedModelEx
    {
        #region Character Storage
        // Index of data keyed for each user - a server process may need to keep many players in memory
        public static Dictionary<string, UserModel> serverUsers = new Dictionary<string, UserModel>();
        // Reference to singleton global user - a client process cannot access more than 1 player's information directly
        public static UserModel globalClientUser = new UserModel();
        #endregion Character Storage

        public const string SWILL_NAME = "swill"; // TODO: This is global information specific to 1 title - Resolve this
        public static string swillItemId; // TODO: This is global information specific to 1 title - Resolve this

        #region Title information
        public static Dictionary<string, string> titleData = new Dictionary<string, string>();
        public static Dictionary<string, string> titleInternalData = new Dictionary<string, string>();
        public static Dictionary<string, string> publisherData = new Dictionary<string, string>(); // There is no non-user publisher internal data

        public static HashSet<string> virutalCurrencyTypes = new HashSet<string>() { "SS", "GS", "ST" }; // Set your vcKeys here
        public static HashSet<string> consumableItemIds = new HashSet<string>();
        public static HashSet<string> containerItemIds = new HashSet<string>();

        // These will be identical, but they are currently different datatypes
        public static Dictionary<string, ServerModels.CatalogItem> serverCatalog = new Dictionary<string, ServerModels.CatalogItem>();
        public static Dictionary<string, ClientModels.CatalogItem> clientCatalog = new Dictionary<string, ClientModels.CatalogItem>();
        #endregion Title information
    }

    public class UserModel
    {
        #region General User Information
        public string playFabId;
        public List<string> characterIds = new List<string>();
        public List<string> characterNames = new List<string>();
        public Dictionary<string, string> userData = new Dictionary<string, string>();
        public Dictionary<string, string> userReadOnlyData = new Dictionary<string, string>();
        public Dictionary<string, string> userInternalData = new Dictionary<string, string>();
        public Dictionary<string, string> userPublisherData = new Dictionary<string, string>();
        public Dictionary<string, string> userPublisherReadOnlyData = new Dictionary<string, string>();
        public Dictionary<string, string> userPublisherInternalData = new Dictionary<string, string>();
        #endregion Login

        #region Shared/Server/Client Inventory
        // Shared
        public string userInvDisplay = "";
        // Server
        public List<ServerModels.ItemInstance> serverUserItems;
        public Dictionary<string, CharacterModel> serverCharacterModels = new Dictionary<string, CharacterModel>();
        // Client
        public List<ClientModels.ItemInstance> clientUserItems;
        public Dictionary<string, CharacterModel> clientCharacterModels = new Dictionary<string, CharacterModel>();
        #endregion Inventory

        #region Shared Virtual Currency
        // NOTE: There is no way to request all vc types presently, so the knowledge must be hard coded
        public Dictionary<string, int> userVC = new Dictionary<string, int>();
        #endregion Virtual Currency

        #region Client Trade
        public List<ClientModels.TradeInfo> openTrades;
        #endregion Client Trade

        #region Data Access Functions
        /// <summary>
        /// Return a vc-price that this character can afford for this item, if possible
        /// </summary>
        public bool GetClientItemPrice(string characterId, string catalogItemId, out string vcKey, out int cost)
        {
            ClientModels.CatalogItem catalogItem;
            vcKey = null;
            cost = 0;

            Dictionary<string, int> wallet = userVC;
            CharacterModel tempModel;
            if (characterId == null)
                wallet = userVC;
            else if (clientCharacterModels.TryGetValue(characterId, out tempModel))
                wallet = tempModel.characterVC;
            else
                return false;

            if (PfSharedModelEx.clientCatalog.TryGetValue(catalogItemId, out catalogItem) && wallet != null)
            {
                foreach (var pair in catalogItem.VirtualCurrencyPrices)
                {
                    int curBalance;
                    if (wallet.TryGetValue(pair.Key, out curBalance) && curBalance > pair.Value)
                    {
                        vcKey = pair.Key;
                        cost = (int)pair.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// This gets a reference to the item in inventory (if it exists else null)
        /// Since it's a reference, any changes made to that reference affect the real inventory
        /// </summary>
        public ClientModels.ItemInstance GetClientItem(string characterId, string itemInstanceId)
        {
            List<ClientModels.ItemInstance> tempClientInventory = null;
            CharacterModel tempCharacter;

            if (characterId == null)
                tempClientInventory = clientUserItems;
            else if (clientCharacterModels.TryGetValue(characterId, out tempCharacter))
                tempClientInventory = tempCharacter is ClientCharacterModel ? null : ((ClientCharacterModel)tempCharacter).inventory;

            if (tempClientInventory != null)
                for (int i = 0; i < tempClientInventory.Count; i++)
                    if (tempClientInventory[i].ItemInstanceId == itemInstanceId)
                        return tempClientInventory[i];
            return null;
        }

        /// <summary>
        /// This gets a reference to the item in inventory (if it exists else null)
        /// Since it's a reference, any changes made to that reference affect the real inventory
        /// </summary>
        public ServerModels.ItemInstance GetServerItem(string characterId, string itemInstanceId)
        {
            List<ServerModels.ItemInstance> tempServerInventory = null;
            CharacterModel tempCharacter;

            if (characterId == null)
                tempServerInventory = serverUserItems;
            else if (serverCharacterModels.TryGetValue(characterId, out tempCharacter))
                tempServerInventory = tempCharacter is ServerCharacterModel ? null : ((ServerCharacterModel)tempCharacter).inventory;

            if (tempServerInventory != null)
                for (int i = 0; i < tempServerInventory.Count; i++)
                    if (tempServerInventory[i].ItemInstanceId == itemInstanceId)
                        return tempServerInventory[i];
            return null;
        }
        #endregion Data Access Functions

        #region Data Modification Functions
        public void RemoveItems(string characterId, HashSet<string> itemInstanceIds)
        {
            if (characterId == null)
            {
                if (serverUserItems != null)
                    for (int i = serverUserItems.Count - 1; i > 0; i--)
                        if (itemInstanceIds.Contains(serverUserItems[i].ItemInstanceId))
                            serverUserItems.RemoveAt(i);
                if (clientUserItems != null)
                    for (int i = clientUserItems.Count - 1; i > 0; i--)
                        if (itemInstanceIds.Contains(clientUserItems[i].ItemInstanceId))
                            clientUserItems.RemoveAt(i);
            }
            else
            {
                CharacterModel tempCharacter;
                if (serverCharacterModels.TryGetValue(characterId, out tempCharacter))
                    tempCharacter.RemoveItems(itemInstanceIds);
                if (clientCharacterModels.TryGetValue(characterId, out tempCharacter))
                    tempCharacter.RemoveItems(itemInstanceIds);
            }
        }

        /// <summary>
        /// Update the number of remaining uses for an item in the inventory
        /// </summary>
        public void UpdateRemainingUses(string characterId, string itemInstanceId, int newValue)
        {
            var clientItem = GetClientItem(characterId, itemInstanceId);
            if (clientItem != null)
                clientItem.RemainingUses = newValue;
            var serverItem = GetServerItem(characterId, itemInstanceId);
            if (serverItem != null)
                serverItem.RemainingUses = newValue;
        }

        /// <summary>
        /// Modify a current VC balance
        /// </summary>
        public void ModifyVcBalance(string characterId, string vcKey, int delta)
        {
            CharacterModel tempChar;
            int vcValue;

            if (characterId == null)
            {
                userVC.TryGetValue(vcKey, out vcValue);
                vcValue += delta;
                userVC[vcKey] = vcValue;
            }
            else
            {
                if (clientCharacterModels.TryGetValue(characterId, out tempChar))
                {
                    tempChar.characterVC.TryGetValue(vcKey, out vcValue);
                    vcValue += delta;
                    tempChar.characterVC[vcKey] = vcValue;
                }
                if (serverCharacterModels.TryGetValue(characterId, out tempChar))
                {
                    tempChar.characterVC.TryGetValue(vcKey, out vcValue);
                    vcValue += delta;
                    tempChar.characterVC[vcKey] = vcValue;
                }
            }
        }

        /// <summary>
        /// Set a current VC balance
        /// </summary>
        public void SetVcBalance(string characterId, string vcKey, int newValue)
        {
            CharacterModel tempChar;

            if (characterId == null)
                userVC[vcKey] = newValue;
            if (characterId != null && clientCharacterModels.TryGetValue(characterId, out tempChar))
                tempChar.characterVC[vcKey] = newValue;
            if (characterId != null && serverCharacterModels.TryGetValue(characterId, out tempChar))
                tempChar.characterVC[vcKey] = newValue;
        }

        public void RemoveTrade(string tradeId)
        {
            foreach (var eachTrade in openTrades)
            {
                if (eachTrade.TradeId != tradeId)
                    continue;

                PfSharedModelEx.globalClientUser.openTrades.Remove(eachTrade);
                return;
            }
        }
        #endregion Data Modification Functions
    }

    /// <summary>
    /// A wrapper for inventory related, character centric, API calls and info
    /// This mostly exists because the characterId needs to be available at all steps in the process, and a class-wrapper avoids most of the Lambda-hell
    /// </summary>
    public abstract class CharacterModel
    {
        public string playFabId;
        public string characterId;
        public string characterName;

        public Dictionary<string, string> characterData = new Dictionary<string, string>();
        public Dictionary<string, string> characterReadOnlyData = new Dictionary<string, string>();
        public Dictionary<string, string> characterInternalData = new Dictionary<string, string>();

        public string inventoryDisplay = "";
        public Dictionary<string, int> characterVC = new Dictionary<string, int>();

        public CharacterModel(string playFabId, string characterId, string characterName)
        {
            this.playFabId = playFabId;
            this.characterId = characterId;
            this.characterName = characterName;
        }

        public abstract void RemoveItems(HashSet<string> itemInstanceIds);
    }

    public class ServerCharacterModel : CharacterModel
    {
        public List<ServerModels.ItemInstance> inventory; // We don't currently have a shared-model between client and server

        public ServerCharacterModel(string playFabId, string characterId, string characterName) : base(playFabId, characterId, characterName) { }

        public override void RemoveItems(HashSet<string> itemInstanceIds)
        {
            if (inventory != null)
                for (int i = inventory.Count - 1; i > 0; i--)
                    if (itemInstanceIds.Contains(inventory[i].ItemInstanceId))
                        inventory.RemoveAt(i);
        }
    }

    public class ClientCharacterModel : CharacterModel
    {
        public List<ClientModels.ItemInstance> inventory; // We don't currently have a shared-model between client and server

        public ClientCharacterModel(string playFabId, string characterId, string characterName) : base(playFabId, characterId, characterName) { }

        public override void RemoveItems(HashSet<string> itemInstanceIds)
        {
            if (inventory != null)
                for (int i = inventory.Count - 1; i > 0; i--)
                    if (itemInstanceIds.Contains(inventory[i].ItemInstanceId))
                        inventory.RemoveAt(i);
        }
    }
}