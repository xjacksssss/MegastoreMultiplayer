using System.Collections.Generic;
using HarmonyLib;
using LiteNetLib;
using LiteNetLib.Utils;
using MegastoreMultiplayer.Messages;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    public static class NetMessages
    {
        private static readonly NetDataWriter _writer = new NetDataWriter();

        // Host-side set of box IDs currently held by any player.
        // Prevents two clients grabbing the same box simultaneously.
        private static readonly System.Collections.Generic.HashSet<int> _lockedBoxes =
            new System.Collections.Generic.HashSet<int>();

        // Tracks which peer (by ID) is carrying each locked box. -1 = host.
        private static readonly System.Collections.Generic.Dictionary<int, int> _boxCarriers =
            new System.Collections.Generic.Dictionary<int, int>();

        public static void LockBox(int boxId, int carrierId = -1)
        {
            _lockedBoxes.Add(boxId);
            _boxCarriers[boxId] = carrierId;
        }
        public static void UnlockBox(int boxId)
        {
            _lockedBoxes.Remove(boxId);
            _boxCarriers.Remove(boxId);
        }
        public static void ClearBoxLocks()
        {
            _lockedBoxes.Clear();
            _boxCarriers.Clear();
        }

        // Returns box IDs currently carried by the given peer so the disconnect handler
        // can broadcast BoxDropped for each one, reactivating them on all clients.
        public static System.Collections.Generic.IEnumerable<int> GetBoxesCarriedBy(int peerId)
        {
            foreach (var kv in _boxCarriers)
                if (kv.Value == peerId) yield return kv.Key;
        }

        // Pallet and tray locks — same contention-prevention logic as boxes.
        private static readonly System.Collections.Generic.HashSet<int> _lockedPallets =
            new System.Collections.Generic.HashSet<int>();
        private static readonly System.Collections.Generic.HashSet<int> _lockedTrays =
            new System.Collections.Generic.HashSet<int>();

        public static void LockPallet(int id)    => _lockedPallets.Add(id);
        public static void UnlockPallet(int id)  => _lockedPallets.Remove(id);
        public static void ClearPalletLocks()    => _lockedPallets.Clear();

        public static void LockTray(int id)      => _lockedTrays.Add(id);
        public static void UnlockTray(int id)    => _lockedTrays.Remove(id);
        public static void ClearTrayLocks()      => _lockedTrays.Clear();

        // ── Write helpers ─────────────────────────────────────────────────────────

        public static NetDataWriter WritePlayerPosition(Vector3 position, Vector3 eulerAngles, int peerId = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PlayerPosition);
            _writer.Put(peerId);
            _writer.Put(position.x);    _writer.Put(position.y);    _writer.Put(position.z);
            _writer.Put(eulerAngles.x); _writer.Put(eulerAngles.y); _writer.Put(eulerAngles.z);
            return _writer;
        }

        public static NetDataWriter WriteShelfUpdate(string shelfId, int productType, int newCount)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ShelfUpdate);
            _writer.Put(shelfId);
            _writer.Put(productType);
            _writer.Put(newCount);
            return _writer;
        }

        public static NetDataWriter WriteMoneyUpdate(float newBalance)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.MoneyUpdate);
            _writer.Put(newBalance);
            return _writer;
        }

        public static NetDataWriter WriteOrderConfirmed(int receivingArea, int[] productTypes, int[] counts, bool[] isPallet)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.OrderConfirmed);
            _writer.Put(receivingArea);
            _writer.Put(productTypes.Length);
            for (int i = 0; i < productTypes.Length; i++)
            {
                _writer.Put(productTypes[i]);
                _writer.Put(counts[i]);
                _writer.Put(isPallet[i]);
            }
            return _writer;
        }

        public static NetDataWriter WriteBoxPicked(int boxId, int productType, int productCount, int peerId = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BoxPicked);
            _writer.Put(peerId);
            _writer.Put(boxId);
            _writer.Put(productType);
            _writer.Put(productCount);
            return _writer;
        }

        public static NetDataWriter WriteVehiclePosition(int typeEnum, int vehicleId, Vector3 pos, Vector3 euler, int peerId = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.VehiclePosition);
            _writer.Put(peerId);
            _writer.Put(typeEnum); _writer.Put(vehicleId);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteBoxThrown(int boxId, Vector3 position, Vector3 velocity, int peerId = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BoxThrown);
            _writer.Put(peerId);
            _writer.Put(boxId);
            _writer.Put(position.x); _writer.Put(position.y); _writer.Put(position.z);
            _writer.Put(velocity.x); _writer.Put(velocity.y); _writer.Put(velocity.z);
            return _writer;
        }

        public static NetDataWriter WriteBoxOnVehicle(int boxId, int vehicleType, int vehicleId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BoxOnVehicle);
            _writer.Put(boxId); _writer.Put(vehicleType); _writer.Put(vehicleId);
            return _writer;
        }

        public static NetDataWriter WriteVehicleUnloaded(int vehicleType, int vehicleId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.VehicleUnloaded);
            _writer.Put(vehicleType); _writer.Put(vehicleId);
            return _writer;
        }

        public static NetDataWriter WriteBoxDropped(int boxId, Vector3 position, Vector3 eulerAngles)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BoxDropped);
            _writer.Put(boxId);
            _writer.Put(position.x);    _writer.Put(position.y);    _writer.Put(position.z);
            _writer.Put(eulerAngles.x); _writer.Put(eulerAngles.y); _writer.Put(eulerAngles.z);
            return _writer;
        }

        public static NetDataWriter WriteOvenStart(int ovenId, float elapsed = 0f)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.OvenStart);
            _writer.Put(ovenId);
            _writer.Put(elapsed); // seconds already cooked; 0 for a brand-new start
            return _writer;
        }

        public static NetDataWriter WriteOvenComplete(int ovenId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.OvenComplete);
            _writer.Put(ovenId);
            return _writer;
        }

        public static NetDataWriter WritePriceUpdate(int productType, float price)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PriceUpdate);
            _writer.Put(productType);
            _writer.Put(price);
            return _writer;
        }

        public static NetDataWriter WritePlaceableMoved(int typeEnum, int id, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PlaceableMoved);
            _writer.Put(typeEnum);
            _writer.Put(id);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteFurnitureMoved(int typeEnum, int id, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.FurnitureMoved);
            _writer.Put(typeEnum);
            _writer.Put(id);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteTutorialStep(int stepId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.TutorialStep);
            _writer.Put(stepId);
            return _writer;
        }

        public static NetDataWriter WriteObjectiveStep(int stepId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ObjectiveStep);
            _writer.Put(stepId);
            return _writer;
        }

        // ── Dispatch ──────────────────────────────────────────────────────────────

        public static void Dispatch(NetPeer sender, NetDataReader reader)
        {
            var type = (MessageType)reader.GetByte();
            switch (type)
            {
                case MessageType.PlayerPosition: ApplyPlayerPosition(sender, reader); break;
                case MessageType.ShelfUpdate:    ApplyShelfUpdate(sender, reader);    break;
                case MessageType.MoneyUpdate:    ApplyMoneyUpdate(sender, reader);    break;
                case MessageType.OrderConfirmed: ApplyOrderConfirmed(sender, reader); break;
                case MessageType.BoxPicked:      ApplyBoxPicked(sender, reader);      break;
                case MessageType.BoxDropped:     ApplyBoxDropped(sender, reader);     break;
                case MessageType.BoxThrown:      ApplyBoxThrown(sender, reader);      break;
                case MessageType.VehiclePosition:ApplyVehiclePosition(sender, reader);break;
                case MessageType.BoxOnVehicle:   ApplyBoxOnVehicle(sender, reader);   break;
                case MessageType.VehicleUnloaded:ApplyVehicleUnloaded(sender, reader); break;
                case MessageType.PriceUpdate:    ApplyPriceUpdate(sender, reader);    break;
                case MessageType.PlaceableMoved: ApplyPlaceableMoved(sender, reader); break;
                case MessageType.FurnitureMoved: ApplyFurnitureMoved(sender, reader); break;
                case MessageType.OvenStart:      ApplyOvenStart(sender, reader);      break;
                case MessageType.OvenComplete:   ApplyOvenComplete(sender, reader);   break;
                case MessageType.TutorialStep:   ApplyTutorialStep(sender, reader);   break;
                case MessageType.ObjectiveStep:  ApplyObjectiveStep(sender, reader);  break;
                case MessageType.StoreOpenClose:  ApplyStoreOpenClose(sender, reader);  break;
                case MessageType.LicensePurchased:ApplyLicensePurchased(sender, reader);break;
                case MessageType.StoreGrowth:     ApplyStoreGrowth(sender, reader);     break;
                case MessageType.DayEnded:        ApplyDayEnded(sender, reader);        break;
                case MessageType.NewDayStarted:   ApplyNewDayStarted(sender, reader);   break;
                case MessageType.TrayPickedUp:    ApplyTrayPickedUp(sender, reader);    break;
                case MessageType.TrayPutDown:     ApplyTrayPutDown(sender, reader);     break;
                case MessageType.LevelUp:         ApplyLevelUp(sender, reader);         break;
                case MessageType.LightToggle:     ApplyLightToggle(sender, reader);     break;
                case MessageType.EmployeeHired:   ApplyEmployeeHired(sender, reader);   break;
                case MessageType.EmployeeFired:   ApplyEmployeeFired(sender, reader);   break;
                case MessageType.NpcPosition:     ApplyNpcPosition(sender, reader);     break;
                case MessageType.NpcSpawn:            ApplyNpcSpawn(sender, reader);            break;
                case MessageType.NpcDespawn:          ApplyNpcDespawn(sender, reader);          break;
                case MessageType.EmployeeActivated:   ApplyEmployeeActivated(sender, reader);   break;
                case MessageType.EmployeeDeactivated: ApplyEmployeeDeactivated(sender, reader); break;
                case MessageType.EmployeePosition:    ApplyEmployeePosition(sender, reader);    break;
                case MessageType.EmployeeUpdated:     ApplyEmployeeUpdated(sender, reader);     break;
                case MessageType.CarSpawn:            ApplyCarSpawn(sender, reader);            break;
                case MessageType.CarPosition:         ApplyCarPosition(sender, reader);         break;
                case MessageType.CarDespawn:          ApplyCarDespawn(sender, reader);          break;
                case MessageType.TruckArrived:        ApplyTruckArrived(sender, reader);        break;
                case MessageType.TruckLeft:           ApplyTruckLeft(sender, reader);           break;
                case MessageType.StoreName:           ApplyStoreName(sender, reader);           break;
                case MessageType.CustomerAnim:        ApplyCustomerAnim(sender, reader);        break;
                case MessageType.CheckoutStep:        ApplyCheckoutStep(sender, reader);        break;
                case MessageType.PlayerName:      ApplyPlayerName(sender, reader);      break;
                case MessageType.MigrationPlan:   ApplyMigrationPlan(sender, reader);   break;
                case MessageType.AdUpdate:        ApplyAdUpdate(sender, reader);        break;
                case MessageType.RecyclingDone:     ApplyRecyclingDone(sender, reader);     break;
                case MessageType.BakeryDoughLoaded: ApplyBakeryDoughLoaded(sender, reader); break;
                case MessageType.PalletPickedUp:  ApplyPalletPickedUp(sender, reader);  break;
                case MessageType.PalletDropped:   ApplyPalletDropped(sender, reader);   break;
                case MessageType.PalletTrashed:   ApplyPalletTrashed(sender, reader);   break;
                case MessageType.SyncCheck:       ApplySyncCheck(sender, reader);       break;
                case MessageType.ResyncRequest:   ApplyResyncRequest(sender, reader);   break;
                case MessageType.XpUpdate:           ApplyXpUpdate(sender, reader);           break;
                case MessageType.VendingMoneyUpdate:  ApplyVendingMoneyUpdate(sender, reader);  break;
                case MessageType.DecorationUsed:      ApplyDecorationUsed(sender, reader);      break;
                case MessageType.ChoppingStandSit:    ApplyChoppingStandSit(sender, reader);    break;
                case MessageType.ChoppingStandLeave:  ApplyChoppingStandLeave(sender, reader);  break;
                case MessageType.ChoppingAskProduct:  ApplyChoppingAskProduct(sender, reader);  break;
                case MessageType.ChoppingProductGiven: ApplyChoppingProductGiven(sender, reader); break;
                case MessageType.CheckoutCustomerQueued:  ApplyCheckoutCustomerQueued(sender, reader);  break;
                case MessageType.CheckoutProductsPlaced:  ApplyCheckoutProductsPlaced(sender, reader);  break;
                case MessageType.CheckoutCustomerLeft:    ApplyCheckoutCustomerLeft(sender, reader);    break;
                case MessageType.CheckoutManualScan:      ApplyCheckoutManualScan(sender, reader);      break;
                case MessageType.CheckoutPaymentCash:     ApplyCheckoutPaymentCash(sender, reader);     break;
                case MessageType.CheckoutPaymentCard:     ApplyCheckoutPaymentCard(sender, reader);     break;
                case MessageType.CheckoutPaymentTaken:    ApplyCheckoutPaymentTaken(sender, reader);    break;
                case MessageType.WandererSpawn:   ApplyWandererSpawn(sender, reader);   break;
                case MessageType.WandererDespawn: ApplyWandererDespawn(sender, reader); break;
                case MessageType.BoxOpened:       ApplyBoxOpened(sender, reader);       break;
                case MessageType.EmployeeAnim:    ApplyEmployeeAnim(sender, reader);    break;
                case MessageType.StateSnapshot:  StateSnapshot.Apply(reader);         break;
                case MessageType.SaveDataSync:   SaveDataSync.Apply(reader);          break;
                default:
                    Plugin.Log.LogWarning($"[Net] Unknown message type: {(byte)type}");
                    break;
            }
        }

        // ── Apply ─────────────────────────────────────────────────────────────────

        private static void ApplyPlayerPosition(NetPeer sender, NetDataReader r)
        {
            int peerId = r.GetInt();
            var pos    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler  = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            int effectiveId = peerId == -1 ? sender.Id : peerId;
            RemotePlayerManager.UpdatePosition(effectiveId, pos, euler);

            if (MultiplayerManager.IsHost)
            {
                var w = WritePlayerPosition(pos, euler, sender.Id);
                MultiplayerManager.SendToAllUnreliableExcept(w, sender);
            }
        }

        private static void ApplyShelfUpdate(NetPeer sender, NetDataReader r)
        {
            string shelfId  = r.GetString();
            int    pType    = r.GetInt();
            int    newCount = r.GetInt();

            var shelf = ShelfRegistry.Get(shelfId);
            if (shelf == null)
            {
                Plugin.Log.LogWarning($"[Net] ShelfUpdate: unknown shelf {shelfId}");
            }
            else
            {
                SyncShelf(shelf, pType, newCount);
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteShelfUpdate(shelfId, pType, newCount), sender);
        }

        private static void ApplyMoneyUpdate(NetPeer sender, NetDataReader r)
        {
            float newBalance = r.GetFloat();
            var em = SingletonBehaviour<EconomyManager>.Instance;
            if (em != null)
                NetApply.Run(() =>
                {
                    Traverse.Create(em).Field("softCurrency").SetValue(newBalance);
                    Traverse.Create(em).Method("SaveSoftCurrency").GetValue();
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteMoneyUpdate(newBalance), sender);
        }

        private static void ApplyOrderConfirmed(NetPeer sender, NetDataReader r)
        {
            int area  = r.GetInt();
            int count = r.GetInt();

            var slots = new List<BuyPanel.CartSlot>();
            for (int i = 0; i < count; i++)
                slots.Add(new BuyPanel.CartSlot((ProductType)r.GetInt(), r.GetInt(), r.GetBool()));

            var om = SingletonBehaviour<OrderManager>.Instance;
            if (om != null)
                NetApply.Run(() => om.AddOrder(slots, (OrderManager.OrderReceivingArea)area));

            if (MultiplayerManager.IsHost && sender != null)
            {
                var types    = new int[slots.Count];
                var counts   = new int[slots.Count];
                var isPallet = new bool[slots.Count];
                for (int i = 0; i < slots.Count; i++)
                {
                    types[i]    = (int)slots[i].type;
                    counts[i]   = slots[i].amount;
                    isPallet[i] = slots[i].isPallet;
                }
                MultiplayerManager.SendToAllReliableExcept(WriteOrderConfirmed(area, types, counts, isPallet), sender);
            }
        }

        private static void ApplyBoxThrown(NetPeer sender, NetDataReader r)
        {
            int peerId = r.GetInt();
            int boxId  = r.GetInt();
            _lockedBoxes.Remove(boxId);
            var pos = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var vel = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            int effectivePeerId = peerId == -1 ? RemotePlayerManager.HostPeerId : peerId;

            var box = RemotePlayerManager.DetachBox(effectivePeerId);
            if (box == null)
            {
                var boxes = StateSnapshot.GetSpawnedBoxes();
                boxes?.TryGetValue(boxId, out box);
            }

            if (box != null)
            {
                box.transform.position       = pos;
                box.gameObject.SetActive(true);
                box.RigidBody.constraints    = RigidbodyConstraints.None;
                box.RigidBody.linearVelocity = vel;
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteBoxThrown(boxId, pos, vel, sender.Id), sender);
        }

        private static void ApplyBoxOnVehicle(NetPeer sender, NetDataReader r)
        {
            int boxId       = r.GetInt();
            _lockedBoxes.Remove(boxId);
            int vehicleType = r.GetInt();
            int vehicleId   = r.GetInt();

            var boxes = StateSnapshot.GetSpawnedBoxes();
            if (boxes == null || !boxes.TryGetValue(boxId, out var box) || box == null) return;

            // Unparent from any avatar carry first.
            var anyPeer = RemotePlayerManager.FindCarrier(boxId);
            if (anyPeer != -1) RemotePlayerManager.DetachBox(anyPeer);

            var vehicle = VehicleRegistry.Get((VehicleType)vehicleType, vehicleId) as HandTruck;
            if (vehicle != null)
                NetApply.Run(() => vehicle.PlaceBoxByClick(box));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteBoxOnVehicle(boxId, vehicleType, vehicleId), sender);
        }

        private static void ApplyVehicleUnloaded(NetPeer sender, NetDataReader r)
        {
            int vehicleType = r.GetInt();
            int vehicleId   = r.GetInt();

            var vehicle = VehicleRegistry.Get((VehicleType)vehicleType, vehicleId) as HandTruck;
            if (vehicle != null)
                NetApply.Run(() => Traverse.Create(vehicle).Method("UnloadBoxes").GetValue());

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteVehicleUnloaded(vehicleType, vehicleId), sender);
        }

        private static void ApplyBoxPicked(NetPeer sender, NetDataReader r)
        {
            int peerId = r.GetInt();
            int boxId  = r.GetInt();
            r.GetInt(); // productType
            r.GetInt(); // count

            // Host: reject if another player already holds this box.
            if (MultiplayerManager.IsHost && sender != null)
            {
                if (_lockedBoxes.Contains(boxId))
                {
                    // Deny — send the box's current world position back as a correction.
                    var allBoxes = StateSnapshot.GetSpawnedBoxes();
                    if (allBoxes != null && allBoxes.TryGetValue(boxId, out var contested) && contested != null)
                        sender.Send(WriteBoxDropped(boxId, contested.transform.position, contested.transform.eulerAngles),
                            LiteNetLib.DeliveryMethod.ReliableOrdered);
                    return;
                }
                LockBox(boxId, sender.Id); // track carrier so disconnect handler can recover it
                MultiplayerManager.SendToAllReliableExcept(WriteBoxPicked(boxId, -1, 0, sender.Id), sender);
            }

            int effectivePeerId = peerId == -1
                ? RemotePlayerManager.HostPeerId
                : peerId;

            var boxes = StateSnapshot.GetSpawnedBoxes();
            if (boxes != null && boxes.TryGetValue(boxId, out var box) && box != null)
                RemotePlayerManager.AttachBox(effectivePeerId, box);
        }

        private static void ApplyBoxDropped(NetPeer sender, NetDataReader r)
        {
            int boxId = r.GetInt();
            _lockedBoxes.Remove(boxId);
            var pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            int effectivePeerId = sender == null ? RemotePlayerManager.HostPeerId : sender.Id;

            var box = RemotePlayerManager.DetachBox(effectivePeerId);
            if (box != null)
            {
                box.transform.position    = pos;
                box.transform.eulerAngles = euler;
            }
            else
            {
                // Box wasn't attached to an avatar — fall back to repositioning by ID.
                var boxes = StateSnapshot.GetSpawnedBoxes();
                if (boxes != null && boxes.TryGetValue(boxId, out var found) && found != null)
                {
                    found.transform.position    = pos;
                    found.transform.eulerAngles = euler;
                    found.gameObject.SetActive(true);
                }
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteBoxDropped(boxId, pos, euler), sender);
        }

        private static void ApplyVehiclePosition(NetPeer sender, NetDataReader r)
        {
            int peerId    = r.GetInt();
            int typeEnum  = r.GetInt();
            int vehicleId = r.GetInt();
            var pos       = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler     = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            var vehicle = VehicleRegistry.Get((VehicleType)typeEnum, vehicleId);
            if (vehicle != null)
            {
                vehicle.transform.position    = pos;
                vehicle.transform.eulerAngles = euler;
            }

            if (MultiplayerManager.IsHost && sender != null)
            {
                var w = WriteVehiclePosition(typeEnum, vehicleId, pos, euler, sender.Id);
                MultiplayerManager.SendToAllUnreliableExcept(w, sender);
            }
        }

        private static void ApplyPriceUpdate(NetPeer sender, NetDataReader r)
        {
            var pType = (ProductType)r.GetInt();
            float price = r.GetFloat();

            var pm = SingletonBehaviour<PriceManager>.Instance;
            if (pm != null)
                NetApply.Run(() =>
                {
                    pm.SetUnitPrice(pType, price);
                    EventManager.NotifyEvent(ProductEvents.PRODUCT_PRICE_CHANGED, pType, price);
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WritePriceUpdate((int)pType, price), sender);
        }

        private static void ApplyPlaceableMoved(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();
            var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            var placeable = PlaceableRegistry.Get((PlaceableType)typeEnum, id);
            if (placeable == null)
                Plugin.Log.LogWarning($"[Net] PlaceableMoved: unknown placeable {typeEnum}/{id}");
            else
                NetApply.Run(() =>
                {
                    placeable.transform.position    = pos;
                    placeable.transform.eulerAngles = euler;
                    placeable.SavePosition();
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WritePlaceableMoved(typeEnum, id, pos, euler), sender);
        }

        private static void ApplyFurnitureMoved(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();
            var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            var furniture = FurnitureRegistry.Get((FurnitureType)typeEnum, id);
            if (furniture == null)
                Plugin.Log.LogWarning($"[Net] FurnitureMoved: unknown furniture {typeEnum}/{id}");
            else
                NetApply.Run(() =>
                {
                    furniture.transform.position    = pos;
                    furniture.transform.eulerAngles = euler;
                    furniture.SavePosition();
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteFurnitureMoved(typeEnum, id, pos, euler), sender);
        }

        private static void ApplyOvenStart(NetPeer sender, NetDataReader r)
        {
            int   ovenId  = r.GetInt();
            float elapsed = r.GetFloat();
            var   oven    = OvenRegistry.Get(ovenId);
            if (oven == null)
            {
                Plugin.Log.LogWarning($"[Net] OvenStart: unknown oven {ovenId}");
            }
            else
            {
                NetApply.Run(() =>
                {
                    oven.TurnOn();
                    // elapsed > 0.5 s means this arrived with meaningful in-flight delay
                    // (or the message was queued) — advance the tray timers so the countdown
                    // stays accurate. Uses the same logic as the StateSnapshot mid-bake path.
                    if (elapsed > 0.5f)
                        StateSnapshot.AdvanceOvenCooking(oven, elapsed);
                });
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteOvenStart(ovenId, elapsed), sender);
        }

        private static void ApplyOvenComplete(NetPeer sender, NetDataReader r)
        {
            int ovenId = r.GetInt();
            var oven   = OvenRegistry.Get(ovenId);
            if (oven == null)
            {
                Plugin.Log.LogWarning($"[Net] OvenComplete: unknown oven {ovenId}");
            }
            else
            {
                NetApply.Run(() =>
                {
                    Traverse.Create(oven).Method("OnCookingFinished").GetValue();
                    // Play the oven's own completion sound on clients so the audio cue is heard.
                    PlayFirstAudioSource(oven.gameObject);
                });
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteOvenComplete(ovenId), sender);
        }

        private static void ApplyTutorialStep(NetPeer sender, NetDataReader r)
        {
            int stepId = r.GetInt();
            NetApply.Run(() => EventManager.NotifyEvent(TutorialEvents.TUTORIAL_STEP_DONE, stepId));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteTutorialStep(stepId), sender);
        }

        private static void ApplyObjectiveStep(NetPeer sender, NetDataReader r)
        {
            int stepId = r.GetInt();
            NetApply.Run(() => EventManager.NotifyEvent(TutorialEvents.OBJECTIVE_STEP_DONE, stepId));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteObjectiveStep(stepId), sender);
        }

        // ── Write helpers (new systems) ───────────────────────────────────────────

        public static NetDataWriter WriteStoreOpenClose(bool isOpen)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.StoreOpenClose); _writer.Put(isOpen); return _writer;
        }

        public static NetDataWriter WriteLicensePurchased(int licenseLevel, int productGroup)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.LicensePurchased); _writer.Put(licenseLevel); _writer.Put(productGroup); return _writer;
        }

        public static NetDataWriter WriteStoreGrowth()
        {
            _writer.Reset(); _writer.Put((byte)MessageType.StoreGrowth); return _writer;
        }

        public static NetDataWriter WriteDayEnded()
        {
            _writer.Reset(); _writer.Put((byte)MessageType.DayEnded); return _writer;
        }

        public static NetDataWriter WriteNewDayStarted()
        {
            _writer.Reset(); _writer.Put((byte)MessageType.NewDayStarted); return _writer;
        }

        public static NetDataWriter WriteTrayPickedUp(int trayId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.TrayPickedUp); _writer.Put(trayId); return _writer;
        }

        public static NetDataWriter WriteTrayPutDown(int trayId, Vector3 pos, Vector3 euler)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.TrayPutDown);
            _writer.Put(trayId);
            _writer.Put(pos.x); _writer.Put(pos.y); _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteLevelUp(int newLevel)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.LevelUp); _writer.Put(newLevel); return _writer;
        }

        public static NetDataWriter WriteLightToggle(bool isOn)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.LightToggle); _writer.Put(isOn); return _writer;
        }

        public static NetDataWriter WriteEmployeeHired(HiringManager.EmployeeStats s)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.EmployeeHired);
            _writer.Put(s.employeeID); _writer.Put(s.employeeName); _writer.Put((int)s.role);
            _writer.Put(s.moveSpeed); _writer.Put(s.workSpeed); _writer.Put(s.dailyWage);
            _writer.Put(s.isEarylyShift); _writer.Put(s.checkoutDeskID);
            _writer.Put(s.includeWarehouse); _writer.Put((int)s.department);
            return _writer;
        }

        public static NetDataWriter WriteEmployeeFired(int employeeId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.EmployeeFired); _writer.Put(employeeId); return _writer;
        }

        // ── Apply (new systems) ───────────────────────────────────────────────────

        private static void ApplyStoreOpenClose(NetPeer sender, NetDataReader r)
        {
            bool isOpen = r.GetBool();
            var label = SingletonBehaviour<OpenCloseLabel>.Instance;
            if (label != null && label.IsOpen != isOpen)
                NetApply.Run(() =>
                {
                    label.OnLabelClicked();
                    PlayFirstAudioSource(label.gameObject);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteStoreOpenClose(isOpen), sender);
        }

        private static void ApplyLicensePurchased(NetPeer sender, NetDataReader r)
        {
            int level = r.GetInt(); int group = r.GetInt();
            LicenseTracker.Record(level, group);
            NetApply.Run(() => ProductLicenseManager.PurchaseLicense(level, (ProductGroup)group));
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteLicensePurchased(level, group), sender);
        }

        private static void ApplyStoreGrowth(NetPeer sender, NetDataReader r)
        {
            GrowthTracker.Record();
            var gm = SingletonBehaviour<GrowthManager>.Instance;
            if (gm != null) NetApply.Run(() => gm.PurchaseGrowth());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteStoreGrowth(), sender);
        }

        private static void ApplyDayEnded(NetPeer sender, NetDataReader r)
        {
            var tm = SingletonBehaviour<TimeManager>.Instance;
            if (tm != null) NetApply.Run(() => tm.OnEndDay());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteDayEnded(), sender);
        }

        private static void ApplyNewDayStarted(NetPeer sender, NetDataReader r)
        {
            var tm = SingletonBehaviour<TimeManager>.Instance;
            if (tm != null) NetApply.Run(() => tm.StartTheNewDay());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteNewDayStarted(), sender);
        }

        private static void ApplyTrayPickedUp(NetPeer sender, NetDataReader r)
        {
            int trayId = r.GetInt();

            // Host: reject if another player already holds this tray.
            if (MultiplayerManager.IsHost && sender != null)
            {
                if (_lockedTrays.Contains(trayId))
                {
                    var tray2 = TrayRegistry.Get(trayId);
                    if (tray2 != null)
                        sender.Send(WriteTrayPutDown(trayId, tray2.transform.position, tray2.transform.eulerAngles),
                            LiteNetLib.DeliveryMethod.ReliableOrdered);
                    return;
                }
                _lockedTrays.Add(trayId);
                MultiplayerManager.SendToAllReliableExcept(WriteTrayPickedUp(trayId), sender);
            }

            var tray = TrayRegistry.Get(trayId);
            if (tray != null) NetApply.Run(() => tray.gameObject.SetActive(false));
        }

        private static void ApplyTrayPutDown(NetPeer sender, NetDataReader r)
        {
            int trayId = r.GetInt();
            _lockedTrays.Remove(trayId);
            var pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var tray  = TrayRegistry.Get(trayId);
            if (tray != null)
                NetApply.Run(() =>
                {
                    tray.transform.position    = pos;
                    tray.transform.eulerAngles = euler;
                    tray.gameObject.SetActive(true);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteTrayPutDown(trayId, pos, euler), sender);
        }

        private static void ApplyLevelUp(NetPeer sender, NetDataReader r)
        {
            int newLevel = r.GetInt();
            var em = SingletonBehaviour<ExperienceManager>.Instance;
            if (em != null)
                NetApply.Run(() =>
                {
                    var t = HarmonyLib.Traverse.Create(em);
                    t.Field("currentLevel").SetValue(newLevel);
                    t.Field("currentXP").SetValue(0);
                    em.CurrentLevel = newLevel;
                    em.CurrentXP    = 0;
                    EventManager.NotifyEvent(EconomyEvents.LEVEL_UP, newLevel);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteLevelUp(newLevel), sender);
        }

        public static NetDataWriter WriteXpUpdate(int xp, int level)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.XpUpdate);
            _writer.Put(xp);
            _writer.Put(level);
            return _writer;
        }

        private static void ApplyXpUpdate(NetPeer sender, NetDataReader r)
        {
            int xp    = r.GetInt();
            int level = r.GetInt();
            var em = SingletonBehaviour<ExperienceManager>.Instance;
            if (em != null)
                NetApply.Run(() =>
                {
                    var t = HarmonyLib.Traverse.Create(em);
                    t.Field("currentXP").SetValue(xp);
                    t.Field("currentLevel").SetValue(level);
                    em.CurrentXP    = xp;
                    em.CurrentLevel = level;
                    t.Method("Repaint").GetValue();
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteXpUpdate(xp, level), sender);
        }

        private static void ApplyLightToggle(NetPeer sender, NetDataReader r)
        {
            bool isOn = r.GetBool();
            var tm = SingletonBehaviour<TimeManager>.Instance;
            if (tm != null)
                NetApply.Run(() =>
                {
                    var ls = HarmonyLib.Traverse.Create(tm).Field("lightSwitch").GetValue<LightSwitch>();
                    if (ls != null) HarmonyLib.Traverse.Create(ls).Field("isOn").SetValue(isOn);
                    tm.RepaintLights(isOn);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteLightToggle(isOn), sender);
        }

        private static void ApplyEmployeeHired(NetPeer sender, NetDataReader r)
        {
            int    empId  = r.GetInt();
            string name   = r.GetString();
            var    role   = (EmployeeRole)r.GetInt();
            int    move   = r.GetInt();
            int    work   = r.GetInt();
            int    wage   = r.GetInt();
            bool   early  = r.GetBool();
            int    deskId = r.GetInt();
            bool   whouse = r.GetBool();
            var    dept   = (ProductGroup)r.GetInt();

            var s = new HiringManager.EmployeeStats(name, role, move, work, wage, deskId);
            s.employeeID       = empId;
            s.isEarylyShift    = early;
            s.checkoutDeskID   = deskId;
            s.includeWarehouse = whouse;
            s.department       = dept;

            var em = SingletonBehaviour<EmployeeManager>.Instance;
            if (em != null) NetApply.Run(() => em.HireEmployee(s));
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteEmployeeHired(s), sender);
        }

        private static void ApplyEmployeeFired(NetPeer sender, NetDataReader r)
        {
            int empId = r.GetInt();
            var em = SingletonBehaviour<EmployeeManager>.Instance;
            if (em != null)
            {
                var stats = em.HiredEmployees.Find(e => e.employeeID == empId);
                if (stats.employeeID == empId)
                    NetApply.Run(() => em.FireEmployee(stats));
            }
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteEmployeeFired(empId), sender);
        }

        // ── Player name ──────────────────────────────────────────────────────────

        public static NetDataWriter WritePlayerName(string name, int peerId = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PlayerName);
            _writer.Put(peerId);
            _writer.Put(name);
            return _writer;
        }

        private static void ApplyPlayerName(NetPeer sender, NetDataReader r)
        {
            int    peerId = r.GetInt();
            string name   = r.GetString();

            int effectiveId = peerId == -1 ? (sender?.Id ?? RemotePlayerManager.HostPeerId) : peerId;
            RemotePlayerManager.SetName(effectiveId, name);

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WritePlayerName(name, sender.Id), sender);
        }

        // ── Migration plan ────────────────────────────────────────────────────────

        public static NetDataWriter WriteMigrationPlan(string nextHostIp, bool youAreNextHost)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.MigrationPlan);
            _writer.Put(nextHostIp);
            _writer.Put(youAreNextHost);
            return _writer;
        }

        private static void ApplyMigrationPlan(NetPeer sender, NetDataReader r)
        {
            string nextHostIp    = r.GetString();
            bool   iAmNextHost   = r.GetBool();
            MultiplayerManager.StoreMigrationPlan(nextHostIp, iAmNextHost);
        }

        // ── NPC sync (real game models, no capsule proxies) ───────────────────────

        public static NetDataWriter WriteNpcPosition(int networkId, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.NpcPosition);
            _writer.Put(networkId);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteNpcSpawn(int networkId, Vector3 position, int poolIndex = -1)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.NpcSpawn);
            _writer.Put(networkId);
            _writer.Put(position.x); _writer.Put(position.y); _writer.Put(position.z);
            _writer.Put(poolIndex); // customer pool index so clients show the same model
            return _writer;
        }

        public static NetDataWriter WriteNpcDespawn(int networkId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.NpcDespawn);
            _writer.Put(networkId);
            return _writer;
        }

        private static void ApplyNpcPosition(NetPeer sender, NetDataReader r)
        {
            int networkId = r.GetInt();
            var pos       = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler     = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.ApplyPosition(networkId, pos, euler);
            // Originates from host only — no relay needed.
        }

        private static void ApplyNpcSpawn(NetPeer sender, NetDataReader r)
        {
            int networkId = r.GetInt();
            var position  = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            int poolIndex = r.GetInt();
            NpcNetworkManager.SpawnCustomer(networkId, position, poolIndex);
        }

        private static void ApplyNpcDespawn(NetPeer sender, NetDataReader r)
        {
            int networkId = r.GetInt();
            NpcNetworkManager.DespawnCustomer(networkId);
        }

        // ── Wanderer (ambient street) sync ────────────────────────────────────────

        public static NetDataWriter WriteWandererSpawn(int poolIdx, Vector3 startPos, Vector3 startEuler, Vector3 targetPos)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.WandererSpawn);
            _writer.Put(poolIdx);
            _writer.Put(startPos.x);   _writer.Put(startPos.y);   _writer.Put(startPos.z);
            _writer.Put(startEuler.x); _writer.Put(startEuler.y); _writer.Put(startEuler.z);
            _writer.Put(targetPos.x);  _writer.Put(targetPos.y);  _writer.Put(targetPos.z);
            return _writer;
        }

        public static NetDataWriter WriteWandererDespawn(int poolIdx)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.WandererDespawn);
            _writer.Put(poolIdx);
            return _writer;
        }

        private static void ApplyWandererSpawn(NetPeer sender, NetDataReader r)
        {
            int poolIdx    = r.GetInt();
            var startPos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var startEuler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var targetPos  = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.SpawnWanderer(poolIdx, startPos, startEuler, targetPos);
        }

        private static void ApplyWandererDespawn(NetPeer sender, NetDataReader r)
        {
            int poolIdx = r.GetInt();
            NpcNetworkManager.DespawnWanderer(poolIdx);
        }

        // ── Employee NPC sync ─────────────────────────────────────────────────────

        public static NetDataWriter WriteEmployeeActivated(int employeeId, Vector3 position)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.EmployeeActivated);
            _writer.Put(employeeId);
            _writer.Put(position.x); _writer.Put(position.y); _writer.Put(position.z);
            return _writer;
        }

        public static NetDataWriter WriteEmployeeDeactivated(int employeeId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.EmployeeDeactivated);
            _writer.Put(employeeId);
            return _writer;
        }

        public static NetDataWriter WriteEmployeePosition(int employeeId, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.EmployeePosition);
            _writer.Put(employeeId);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        private static void ApplyEmployeeActivated(NetPeer sender, NetDataReader r)
        {
            int employeeId = r.GetInt();
            var position   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.SpawnEmployee(employeeId, position);
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteEmployeeActivated(employeeId, position), sender);
        }

        private static void ApplyEmployeeDeactivated(NetPeer sender, NetDataReader r)
        {
            int employeeId = r.GetInt();
            NpcNetworkManager.DespawnEmployee(employeeId);
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteEmployeeDeactivated(employeeId), sender);
        }

        private static void ApplyEmployeePosition(NetPeer sender, NetDataReader r)
        {
            int employeeId = r.GetInt();
            var pos        = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.ApplyEmployeePosition(employeeId, pos, euler);
            // Originates from host only — no relay needed.
        }

        // ── Offers / promotions ──────────────────────────────────────────────────

        public static NetDataWriter WriteAdUpdate(bool isActive)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.AdUpdate);
            _writer.Put(isActive);
            return _writer;
        }

        private static void ApplyAdUpdate(NetPeer sender, NetDataReader r)
        {
            bool isActive = r.GetBool();
            var  om       = SingletonBehaviour<OfferManager>.Instance;
            if (om != null)
                NetApply.Run(() =>
                {
                    if (isActive) om.ActivateOffer();
                    else          Traverse.Create(om).Method("DeactivateOffer").GetValue();
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteAdUpdate(isActive), sender);
        }


        // ── Recycling (Trash) ─────────────────────────────────────────────────────

        public static NetDataWriter WriteRecyclingDone(int boxId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.RecyclingDone);
            _writer.Put(boxId);
            return _writer;
        }

        private static void ApplyRecyclingDone(NetPeer sender, NetDataReader r)
        {
            int boxId = r.GetInt();
            _lockedBoxes.Remove(boxId);
            // Hide the box on this client — it was trashed on the sender's side.
            var boxes = StateSnapshot.GetSpawnedBoxes();
            if (boxes != null && boxes.TryGetValue(boxId, out var box) && box != null)
                NetApply.Run(() => box.gameObject.SetActive(false));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteRecyclingDone(boxId), sender);
        }

        // ── Bakery dough ─────────────────────────────────────────────────────────

        public static NetDataWriter WriteBakeryDoughLoaded(int ovenId, int trayId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BakeryDoughLoaded);
            _writer.Put(ovenId);
            _writer.Put(trayId);
            return _writer;
        }

        private static void ApplyBakeryDoughLoaded(NetPeer sender, NetDataReader r)
        {
            int ovenId = r.GetInt();
            int trayId = r.GetInt();
            var oven   = OvenRegistry.Get(ovenId);
            var tray   = TrayRegistry.Get(trayId);
            if (oven != null && tray != null)
                NetApply.Run(() => oven.PutTrayBack(tray));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteBakeryDoughLoaded(ovenId, trayId), sender);
        }

        // ── Employee update ───────────────────────────────────────────────────────

        public static NetDataWriter WriteEmployeeUpdated(HiringManager.EmployeeStats s)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.EmployeeUpdated);
            _writer.Put(s.employeeID);
            _writer.Put(s.employeeName);
            _writer.Put((int)s.role);
            _writer.Put(s.moveSpeed);
            _writer.Put(s.workSpeed);
            _writer.Put(s.dailyWage);
            _writer.Put(s.isEarylyShift);
            _writer.Put(s.checkoutDeskID);
            _writer.Put(s.includeWarehouse);
            _writer.Put((int)s.department);
            return _writer;
        }

        private static void ApplyEmployeeUpdated(NetPeer sender, NetDataReader r)
        {
            int    empId  = r.GetInt();
            string name   = r.GetString();
            var    role   = (EmployeeRole)r.GetInt();
            int    move   = r.GetInt();
            int    work   = r.GetInt();
            int    wage   = r.GetInt();
            bool   early  = r.GetBool();
            int    deskId = r.GetInt();
            bool   whouse = r.GetBool();
            var    dept   = (ProductGroup)r.GetInt();

            var s = new HiringManager.EmployeeStats(name, role, move, work, wage, deskId);
            s.employeeID       = empId;
            s.isEarylyShift    = early;
            s.checkoutDeskID   = deskId;
            s.includeWarehouse = whouse;
            s.department       = dept;

            var em = SingletonBehaviour<EmployeeManager>.Instance;
            if (em != null) NetApply.Run(() => em.UpdateEmployeeData(s));
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteEmployeeUpdated(s), sender);
        }

        // ── Customer cars ─────────────────────────────────────────────────────────

        public static NetDataWriter WriteCarSpawn(int lotIndex, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CarSpawn);
            _writer.Put(lotIndex);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteCarPosition(int lotIndex, Vector3 pos, Vector3 euler)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CarPosition);
            _writer.Put(lotIndex);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            return _writer;
        }

        public static NetDataWriter WriteCarDespawn(int lotIndex)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CarDespawn);
            _writer.Put(lotIndex);
            return _writer;
        }

        private static void ApplyCarSpawn(NetPeer sender, NetDataReader r)
        {
            int lotIndex = r.GetInt();
            var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.SpawnCar(lotIndex, pos, euler);
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCarSpawn(lotIndex, pos, euler), sender);
        }

        private static void ApplyCarPosition(NetPeer sender, NetDataReader r)
        {
            int lotIndex = r.GetInt();
            var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            NpcNetworkManager.ApplyCarPosition(lotIndex, pos, euler);
        }

        private static void ApplyCarDespawn(NetPeer sender, NetDataReader r)
        {
            int lotIndex = r.GetInt();
            NpcNetworkManager.DespawnCar(lotIndex);
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCarDespawn(lotIndex), sender);
        }

        // ── Trucks ────────────────────────────────────────────────────────────────

        public static NetDataWriter WriteTruckArrived(int areaIndex)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.TruckArrived);
            _writer.Put(areaIndex);
            return _writer;
        }

        public static NetDataWriter WriteTruckLeft(int areaIndex)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.TruckLeft);
            _writer.Put(areaIndex);
            return _writer;
        }

        private static void ApplyTruckArrived(NetPeer sender, NetDataReader r)
        {
            int areaIndex = r.GetInt();
            var tm = SingletonBehaviour<TruckManager>.Instance;
            var truck = tm?.GetTruck((OrderManager.OrderReceivingArea)areaIndex);
            if (truck != null)
                NetApply.Run(() =>
                {
                    truck.Activate();
                    PlayFirstAudioSource(truck.gameObject);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteTruckArrived(areaIndex), sender);
        }

        private static void ApplyTruckLeft(NetPeer sender, NetDataReader r)
        {
            int areaIndex = r.GetInt();
            var tm = SingletonBehaviour<TruckManager>.Instance;
            var truck = tm?.GetTruck((OrderManager.OrderReceivingArea)areaIndex);
            if (truck != null) NetApply.Run(() => truck.Leave());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteTruckLeft(areaIndex), sender);
        }

        // ── Store name ────────────────────────────────────────────────────────────

        public static NetDataWriter WriteStoreName(string name)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.StoreName);
            _writer.Put(name);
            return _writer;
        }

        private static void ApplyStoreName(NetPeer sender, NetDataReader r)
        {
            string name = r.GetString();
            // Persist to disk so the window shows the correct name if opened.
            GenericDataSerializer.SaveString(StartWindow.supermarketNameKey, name);
            // Update the live 3D signs if the window is loaded.
            var window = Object.FindAnyObjectByType<SupermarketNameWindow>();
            if (window != null)
                NetApply.Run(() =>
                {
                    Traverse.Create(window).Field("currentName").SetValue(name);
                    Traverse.Create(window).Method("ApplyName", name).GetValue();
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteStoreName(name), sender);
        }

        // ── Customer animations ───────────────────────────────────────────────────

        public static NetDataWriter WriteCustomerAnim(int networkId, string trigger)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CustomerAnim);
            _writer.Put(networkId);
            _writer.Put(trigger);
            return _writer;
        }

        private static void ApplyCustomerAnim(NetPeer sender, NetDataReader r)
        {
            int    networkId = r.GetInt();
            string trigger   = r.GetString();
            NpcNetworkManager.TriggerCustomerAnim(networkId, trigger);
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCustomerAnim(networkId, trigger), sender);
        }

        // ── Checkout step ─────────────────────────────────────────────────────────

        public static NetDataWriter WriteCheckoutStep(int furnitureId, float speedMultiplier)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CheckoutStep);
            _writer.Put(furnitureId);
            _writer.Put(speedMultiplier);
            return _writer;
        }

        private static void ApplyCheckoutStep(NetPeer sender, NetDataReader r)
        {
            int   furnitureId    = r.GetInt();
            float speedMultiplier = r.GetFloat();

            var managers = Object.FindObjectsByType<CheckoutManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                if (m.FurnitureID != furnitureId) continue;
                NetApply.Run(() => m.CheckoutStep(speedMultiplier));
                break;
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutStep(furnitureId, speedMultiplier), sender);
        }

        // ── Pallets ───────────────────────────────────────────────────────────────

        public static NetDataWriter WritePalletPickedUp(int vehicleType, int vehicleId, int palletId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PalletPickedUp);
            _writer.Put(vehicleType);
            _writer.Put(vehicleId);
            _writer.Put(palletId);
            return _writer;
        }

        public static NetDataWriter WritePalletDropped(int palletId, Vector3 pos, Vector3 euler, Vector3 velocity = default)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PalletDropped);
            _writer.Put(palletId);
            _writer.Put(pos.x);   _writer.Put(pos.y);   _writer.Put(pos.z);
            _writer.Put(euler.x); _writer.Put(euler.y); _writer.Put(euler.z);
            _writer.Put(velocity.x); _writer.Put(velocity.y); _writer.Put(velocity.z);
            return _writer;
        }

        public static NetDataWriter WritePalletTrashed(int palletId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.PalletTrashed);
            _writer.Put(palletId);
            return _writer;
        }

        private static void ApplyPalletPickedUp(NetPeer sender, NetDataReader r)
        {
            int vehicleType = r.GetInt();
            int vehicleId   = r.GetInt();
            int palletId    = r.GetInt();

            // Host: reject if another player already holds this pallet.
            if (MultiplayerManager.IsHost && sender != null)
            {
                if (_lockedPallets.Contains(palletId))
                {
                    var pm2    = SingletonBehaviour<PalletManager>.Instance;
                    var pallet2 = pm2?.GetPallet(palletId);
                    if (pallet2 != null)
                        sender.Send(WritePalletDropped(palletId, pallet2.transform.position, pallet2.transform.eulerAngles),
                            LiteNetLib.DeliveryMethod.ReliableOrdered);
                    return;
                }
                _lockedPallets.Add(palletId);
                MultiplayerManager.SendToAllReliableExcept(WritePalletPickedUp(vehicleType, vehicleId, palletId), sender);
            }

            // Hide the pallet from the world — it's now on the vehicle/player.
            var pm     = SingletonBehaviour<PalletManager>.Instance;
            var pallet = pm?.GetPallet(palletId);
            if (pallet != null)
                NetApply.Run(() => pallet.EnableCollider(false));
        }

        private static void ApplyPalletDropped(NetPeer sender, NetDataReader r)
        {
            int palletId = r.GetInt();
            _lockedPallets.Remove(palletId);
            var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
            var vel      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());

            var pm     = SingletonBehaviour<PalletManager>.Instance;
            var pallet = pm?.GetPallet(palletId);
            if (pallet != null)
                NetApply.Run(() =>
                {
                    pallet.transform.position    = pos;
                    pallet.transform.eulerAngles = euler;
                    pallet.EnableCollider(true);
                    var rb = pallet.GetComponent<Rigidbody>();
                    if (rb != null && vel != Vector3.zero)
                        rb.linearVelocity = vel;
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WritePalletDropped(palletId, pos, euler, vel), sender);
        }

        private static void ApplyPalletTrashed(NetPeer sender, NetDataReader r)
        {
            int palletId = r.GetInt();
            _lockedPallets.Remove(palletId);
            var pm       = SingletonBehaviour<PalletManager>.Instance;
            if (pm != null)
                NetApply.Run(() => pm.DeletePallet(palletId));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WritePalletTrashed(palletId), sender);
        }

        // ── Desync detection ──────────────────────────────────────────────────────

        public static NetDataWriter WriteSyncCheck(int hash)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.SyncCheck);
            _writer.Put(hash);
            return _writer;
        }

        public static NetDataWriter WriteResyncRequest()
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ResyncRequest);
            return _writer;
        }

        private static void ApplySyncCheck(NetPeer sender, NetDataReader r)
        {
            int hostHash = r.GetInt();
            DesyncDetector.CheckHash(hostHash);
        }

        private static void ApplyResyncRequest(NetPeer sender, NetDataReader r)
        {
            // Only the host handles resync requests.
            if (!MultiplayerManager.IsHost || sender == null) return;
            Plugin.Log.LogInfo($"[Desync] Resync requested by peer {sender.Id} — re-sending StateSnapshot.");
            sender.Send(StateSnapshot.Build(), LiteNetLib.DeliveryMethod.ReliableOrdered);
        }

        // ── Checkout desk ────────────────────────────────────────────────────────

        public static NetDataWriter WriteCheckoutCustomerQueued(int furnitureId, int customerNetId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CheckoutCustomerQueued);
            _writer.Put(furnitureId); _writer.Put(customerNetId);
            return _writer;
        }

        public static NetDataWriter WriteCheckoutProductsPlaced(int furnitureId, int[] productTypes, float[] prices)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CheckoutProductsPlaced);
            _writer.Put(furnitureId);
            _writer.Put(productTypes.Length);
            for (int i = 0; i < productTypes.Length; i++) { _writer.Put(productTypes[i]); _writer.Put(prices[i]); }
            return _writer;
        }

        public static NetDataWriter WriteCheckoutCustomerLeft(int furnitureId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.CheckoutCustomerLeft); _writer.Put(furnitureId); return _writer;
        }

        public static NetDataWriter WriteCheckoutManualScan(int furnitureId, int productIndex)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.CheckoutManualScan);
            _writer.Put(furnitureId); _writer.Put(productIndex);
            return _writer;
        }

        public static NetDataWriter WriteCheckoutPaymentCash(int furnitureId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.CheckoutPaymentCash); _writer.Put(furnitureId); return _writer;
        }

        public static NetDataWriter WriteCheckoutPaymentCard(int furnitureId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.CheckoutPaymentCard); _writer.Put(furnitureId); return _writer;
        }

        public static NetDataWriter WriteCheckoutPaymentTaken(int furnitureId)
        {
            _writer.Reset(); _writer.Put((byte)MessageType.CheckoutPaymentTaken); _writer.Put(furnitureId); return _writer;
        }

        private static CheckoutManager FindCheckout(int furnitureId)
        {
            foreach (var m in Object.FindObjectsByType<CheckoutManager>(FindObjectsSortMode.None))
                if (m.FurnitureID == furnitureId) return m;
            return null;
        }

        private static void ApplyCheckoutCustomerQueued(NetPeer sender, NetDataReader r)
        {
            int furnitureId   = r.GetInt();
            int customerNetId = r.GetInt();

            var mgr      = FindCheckout(furnitureId);
            var customer = NpcNetworkManager.GetClientCustomer(customerNetId);
            if (mgr != null && customer != null)
                NetApply.Run(() => mgr.GetIntoTheQueue(customer));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutCustomerQueued(furnitureId, customerNetId), sender);
        }

        private static void ApplyCheckoutProductsPlaced(NetPeer sender, NetDataReader r)
        {
            int furnitureId = r.GetInt();
            int count       = r.GetInt();
            var types       = new int[count];
            var prices      = new float[count];
            for (int i = 0; i < count; i++) { types[i] = r.GetInt(); prices[i] = r.GetFloat(); }

            var mgr  = FindCheckout(furnitureId);
            var pool = SingletonBehaviour<ProductPool>.Instance;
            if (mgr == null || pool == null) return;

            var products = new System.Collections.Generic.List<Product>();
            for (int i = 0; i < count; i++)
            {
                var p = pool.GetProduct((ProductType)types[i]);
                // Set TempPrice via Traverse so ScanProduct gets the right checkout total.
                Traverse.Create(p).Field("tempPrice").SetValue(prices[i]);
                products.Add(p);
            }

            NetApply.Run(() => mgr.PlaceProducts(products));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutProductsPlaced(furnitureId, types, prices), sender);
        }

        private static void ApplyCheckoutCustomerLeft(NetPeer sender, NetDataReader r)
        {
            int furnitureId = r.GetInt();
            var mgr = FindCheckout(furnitureId);
            if (mgr != null) NetApply.Run(() => mgr.OnLeaveStarted());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutCustomerLeft(furnitureId), sender);
        }

        private static void ApplyCheckoutManualScan(NetPeer sender, NetDataReader r)
        {
            int furnitureId  = r.GetInt();
            int productIndex = r.GetInt();

            var mgr = FindCheckout(furnitureId);
            if (mgr != null)
            {
                var placed = Traverse.Create(mgr).Field("productsPlaced").GetValue<System.Collections.Generic.List<Product>>();
                if (placed != null && productIndex < placed.Count)
                    NetApply.Run(() => Traverse.Create(mgr).Method("MoveToBag", new object[] { placed[productIndex], 1f }).GetValue());
            }

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutManualScan(furnitureId, productIndex), sender);
        }

        private static void ApplyCheckoutPaymentCash(NetPeer sender, NetDataReader r)
        {
            int furnitureId = r.GetInt();
            var mgr = FindCheckout(furnitureId);
            if (mgr != null) NetApply.Run(() => mgr.TakeCashPayment());
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutPaymentCash(furnitureId), sender);
        }

        private static void ApplyCheckoutPaymentCard(NetPeer sender, NetDataReader r)
        {
            int furnitureId = r.GetInt();
            var mgr = FindCheckout(furnitureId);
            if (mgr != null)
                NetApply.Run(() =>
                {
                    // Create a transient dummy credit card Transform for the visual animation.
                    // The real card visual lives on the customer NPC which is synced separately.
                    var dummy = new GameObject("_CCDummy").transform;
                    mgr.TakeCardPayment(dummy);
                    Object.Destroy(dummy.gameObject, 3f);
                });
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutPaymentCard(furnitureId), sender);
        }

        private static void ApplyCheckoutPaymentTaken(NetPeer sender, NetDataReader r)
        {
            int furnitureId = r.GetInt();
            var mgr = FindCheckout(furnitureId);
            if (mgr == null) return;

            if (MultiplayerManager.IsHost && sender != null)
            {
                // Client took payment — finalise the NPC transaction on the host.
                // We call OnPaymentFinished directly (via Traverse) to run the NPC
                // cleanup (customer.PaymentDone, queue update, stats) without re-firing
                // the money event (the client already sent a MoneyUpdate via EconomyPatches).
                float price = Traverse.Create(mgr).Field("checkoutPrice").GetValue<float>();
                NetApply.Run(() => Traverse.Create(mgr).Method("OnPaymentFinished", new object[] { price }).GetValue());
                MultiplayerManager.SendToAllReliableExcept(WriteCheckoutPaymentTaken(furnitureId), sender);
            }
            else
            {
                // Host player took payment — run full local cleanup on the client.
                float price = Traverse.Create(mgr).Field("checkoutPrice").GetValue<float>();
                NetApply.Run(() => Traverse.Create(mgr).Method("OnPaymentFinished", new object[] { price }).GetValue());
            }
        }

        // ── Vending machine money ─────────────────────────────────────────────────

        public static NetDataWriter WriteVendingMoneyUpdate(int typeEnum, int id, float storedMoney)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.VendingMoneyUpdate);
            _writer.Put(typeEnum); _writer.Put(id); _writer.Put(storedMoney);
            return _writer;
        }

        private static void ApplyVendingMoneyUpdate(NetPeer sender, NetDataReader r)
        {
            int   typeEnum    = r.GetInt();
            int   id          = r.GetInt();
            float storedMoney = r.GetFloat();

            var vp = PlaceableRegistry.Get((PlaceableType)typeEnum, id) as VendingPlaceable;
            if (vp != null)
                NetApply.Run(() =>
                {
                    Traverse.Create(vp).Field("storedMoney").SetValue(storedMoney);
                    Traverse.Create(vp).Field("readyUI").GetValue<GameObject>()?.SetActive(storedMoney > 0f);
                    Traverse.Create(vp).Field("productInfoUI").GetValue<GameObject>()?.SetActive(storedMoney <= 0f);
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteVendingMoneyUpdate(typeEnum, id, storedMoney), sender);
        }

        // ── Decoration ────────────────────────────────────────────────────────────

        public static NetDataWriter WriteDecorationUsed(int typeEnum, int id)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.DecorationUsed);
            _writer.Put(typeEnum); _writer.Put(id);
            return _writer;
        }

        private static void ApplyDecorationUsed(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();

            var dm = SingletonBehaviour<DecorationManager>.Instance;
            if (dm != null)
                NetApply.Run(() => dm.UseDecoration((DecorationUI.DecorationType)typeEnum, id));

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteDecorationUsed(typeEnum, id), sender);
        }

        // ── Chopping stand ────────────────────────────────────────────────────────

        public static NetDataWriter WriteChoppingStandSit(int typeEnum, int id)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ChoppingStandSit);
            _writer.Put(typeEnum); _writer.Put(id);
            return _writer;
        }

        public static NetDataWriter WriteChoppingStandLeave(int typeEnum, int id)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ChoppingStandLeave);
            _writer.Put(typeEnum); _writer.Put(id);
            return _writer;
        }

        public static NetDataWriter WriteChoppingAskProduct(int typeEnum, int id, int productType, int count, float price, int customerNetId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ChoppingAskProduct);
            _writer.Put(typeEnum); _writer.Put(id);
            _writer.Put(productType); _writer.Put(count); _writer.Put(price); _writer.Put(customerNetId);
            return _writer;
        }

        public static NetDataWriter WriteChoppingProductGiven(int typeEnum, int id)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.ChoppingProductGiven);
            _writer.Put(typeEnum); _writer.Put(id);
            return _writer;
        }

        private static void ApplyChoppingStandSit(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();
            // Lock the stand so remote players can't also sit at it.
            var stand = PlaceableRegistry.Get((PlaceableType)typeEnum, id) as ChoppingStandPlaceable;
            if (stand != null)
                NetApply.Run(() => Traverse.Create(stand).Field("playerSitting").SetValue(true));
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteChoppingStandSit(typeEnum, id), sender);
        }

        private static void ApplyChoppingStandLeave(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();
            var stand = PlaceableRegistry.Get((PlaceableType)typeEnum, id) as ChoppingStandPlaceable;
            if (stand != null)
                NetApply.Run(() => Traverse.Create(stand).Field("playerSitting").SetValue(false));
            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteChoppingStandLeave(typeEnum, id), sender);
        }

        private static void ApplyChoppingAskProduct(NetPeer sender, NetDataReader r)
        {
            int   typeEnum   = r.GetInt();
            int   id         = r.GetInt();
            int   pType      = r.GetInt();
            int   count      = r.GetInt();
            float price      = r.GetFloat();
            int   customerNetId = r.GetInt();

            var stand = PlaceableRegistry.Get((PlaceableType)typeEnum, id) as ChoppingStandPlaceable;
            if (stand == null) return;

            var pool      = SingletonBehaviour<ProductPool>.Instance;
            var products  = new System.Collections.Generic.List<Product>();
            if (pool != null)
                for (int i = 0; i < count; i++)
                    products.Add(pool.GetProduct((ProductType)pType));

            // Find the client-side NPC proxy so GivePreparedProduct can complete locally.
            var customer = NpcNetworkManager.GetClientCustomer(customerNetId);

            NetApply.Run(() => stand.AskForProduct(products, customer));
            // Host-originated only; no relay needed.
        }

        private static void ApplyChoppingProductGiven(NetPeer sender, NetDataReader r)
        {
            int typeEnum = r.GetInt();
            int id       = r.GetInt();

            var stand = PlaceableRegistry.Get((PlaceableType)typeEnum, id) as ChoppingStandPlaceable;
            if (stand == null) return;

            if (MultiplayerManager.IsHost && sender != null)
            {
                // Client told us the product is ready — complete the NPC transaction on the host.
                var foodTray = Traverse.Create(stand).Field("foodTrayProduct").GetValue<FoodTrayProduct>();
                NetApply.Run(() => stand.GivePreparedProduct(foodTray));
            }
            else
            {
                // Host confirmed completion — clear the stand on the client.
                NetApply.Run(() =>
                {
                    Traverse.Create(stand).Field("currentProducts").SetValue(new System.Collections.Generic.List<Product>());
                    Traverse.Create(stand).Field("currentProductIndex").SetValue(0);
                    Traverse.Create(stand).Field("foodTrayProduct").SetValue(null);
                    Traverse.Create(stand).Field("awaitingCustomer").SetValue(null);
                });
            }
        }

        // ── Box opened ────────────────────────────────────────────────────────────

        public static NetDataWriter WriteBoxOpened(int boxId)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.BoxOpened);
            _writer.Put(boxId);
            return _writer;
        }

        private static void ApplyBoxOpened(NetPeer sender, NetDataReader r)
        {
            int boxId = r.GetInt();
            var boxes = StateSnapshot.GetSpawnedBoxes();
            if (boxes != null && boxes.TryGetValue(boxId, out var box) && box != null)
                NetApply.Run(() =>
                {
                    // Attempt to call the game's own Open method via reflection so the lid
                    // animation and visual state update on remote clients.
                    // Only invoke zero-parameter overloads — calling Invoke(box, null) on a
                    // method that expects arguments throws TargetParameterCountException, which
                    // propagates out of NetApply.Run and kills the LiteNetLib receive pump.
                    foreach (var name in new[] { "Open", "OpenBox", "SetOpened" })
                    {
                        var m = HarmonyLib.AccessTools.Method(box.GetType(), name);
                        if (m == null || m.GetParameters().Length != 0) continue;
                        try { m.Invoke(box, null); } catch (System.Exception ex)
                        { Plugin.Log.LogWarning($"[Net] BoxOpened.{name} threw: {ex.Message}"); }
                        break;
                    }
                });

            if (MultiplayerManager.IsHost && sender != null)
                MultiplayerManager.SendToAllReliableExcept(WriteBoxOpened(boxId), sender);
        }

        // ── Employee animation ────────────────────────────────────────────────────

        public static NetDataWriter WriteEmployeeAnim(int employeeId, string trigger)
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.EmployeeAnim);
            _writer.Put(employeeId);
            _writer.Put(trigger);
            return _writer;
        }

        private static void ApplyEmployeeAnim(NetPeer sender, NetDataReader r)
        {
            int    employeeId = r.GetInt();
            string trigger    = r.GetString();
            NpcNetworkManager.TriggerEmployeeAnim(employeeId, trigger);
            // Host-originated; no relay needed.
        }

        // ── Audio helper ──────────────────────────────────────────────────────────

        // Plays the first AudioSource found on the given GameObject (or its children).
        // Used to replay ambient game sounds (oven ding, truck horn, door chime) on
        // clients when those sounds originate from game events that only fire on the host.
        private static void PlayFirstAudioSource(GameObject go)
        {
            if (go == null) return;
            var src = go.GetComponentInChildren<AudioSource>();
            src?.Play();
        }

        // ── Shared sync helper ────────────────────────────────────────────────────

        private static void SyncShelf(Shelf shelf, int productType, int targetCount)
        {
            var pool = SingletonBehaviour<ProductPool>.Instance;
            var type = (ProductType)productType;

            if (shelf.ContainedType != ProductType.NONE && shelf.ContainedType != type)
            {
                while (shelf.GetProductCount() > 0)
                {
                    var p = shelf.GetRandomProduct();
                    if (p == null) break;
                    NetApply.Run(() => shelf.RemoveProduct(p));
                    pool.PutBackToPool(p);
                }
            }

            int delta = targetCount - shelf.GetProductCount();
            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                {
                    var p = pool.GetProduct(type);
                    NetApply.Run(() => shelf.PlaceProduct(p));
                }
            }
            else if (delta < 0)
            {
                for (int i = 0; i < -delta; i++)
                {
                    var p = shelf.GetRandomProduct();
                    if (p == null) break;
                    NetApply.Run(() => shelf.RemoveProduct(p));
                    pool.PutBackToPool(p);
                }
            }
        }
    }
}
