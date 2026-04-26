using System.Collections.Generic;
using DG.Tweening;
using HarmonyLib;
using LiteNetLib.Utils;
using MegastoreMultiplayer.Messages;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Sent by the host to a newly joining client to bring them up to date.
    public static class StateSnapshot
    {
        private static readonly NetDataWriter _writer = new NetDataWriter();

        // Bytes of a snapshot received while the game world wasn't loaded yet.
        // Applied by ApplyPendingIfAny() once StockManager.Initialize() fires.
        private static byte[] _pendingBytes;

        // ── Build (host side) ────────────────────────────────────────────────────

        public static NetDataWriter Build()
        {
            _writer.Reset();
            _writer.Put((byte)MessageType.StateSnapshot);

            WriteMoney();
            WriteShelves();
            WriteBoxes();
            WriteOrders();
            WritePrices();
            WriteLicenses();
            WriteGrowthState();
            WriteEmployees();
            WriteStoreOpenState();
            WriteFurniturePositions();
            WritePlaceablePositions();
            WriteOvenStates();
            WriteTrayStates();
            WriteVehiclePositions();
            WritePallets();
            NpcNetworkManager.RegisterAllActive();  // capture any NPCs that pre-date hosting
            NpcNetworkManager.WriteToSnapshot(_writer);
            WriteOfferState();
            WriteTimeOfDay();
            WriteStoreName();
            WriteExperience();
            WriteVendingMachines();
            WriteDecoration();
            WriteCheckoutState();

            Plugin.Log.LogInfo("[Net] Built StateSnapshot.");
            return _writer;
        }

        private static void WriteMoney()
        {
            float balance = SingletonBehaviour<EconomyManager>.Instance?.SoftCurrency ?? 0f;
            _writer.Put(balance);
        }

        private static void WriteShelves()
        {
            var populated = new List<Shelf>();
            foreach (var s in ShelfRegistry.All)
                if (s.ContainedType != ProductType.NONE && s.GetProductCount() > 0)
                    populated.Add(s);

            _writer.Put(populated.Count);
            foreach (var s in populated)
            {
                _writer.Put(ShelfRegistry.GetId(s));
                _writer.Put((int)s.ContainedType);
                _writer.Put(s.GetProductCount());
            }
        }

        private static void WriteBoxes()
        {
            var boxes = GetSpawnedBoxes();
            if (boxes == null) { _writer.Put(0); return; }

            // Include ALL non-empty boxes: both stored (on ground) and held (carried).
            // The isStored flag lets the client hide carried boxes immediately rather than
            // showing them as ghost objects on the floor.
            var nonEmpty = new List<Box>();
            foreach (var b in boxes.Values)
                if (b != null && !b.IsEmpty()) nonEmpty.Add(b);

            _writer.Put(nonEmpty.Count);
            foreach (var b in nonEmpty)
            {
                _writer.Put(b.BoxID);
                _writer.Put(b.Stored); // false = currently being carried
                _writer.Put(b.transform.position.x);
                _writer.Put(b.transform.position.y);
                _writer.Put(b.transform.position.z);
                _writer.Put(b.transform.eulerAngles.x);
                _writer.Put(b.transform.eulerAngles.y);
                _writer.Put(b.transform.eulerAngles.z);
            }
        }

        private static void WriteOrders()
        {
            var om = SingletonBehaviour<OrderManager>.Instance;
            _writer.Put(5);
            for (int i = 0; i < 5; i++)
            {
                var area   = (OrderManager.OrderReceivingArea)i;
                var orders = om?.GetOrders(area) ?? new List<BuyPanel.CartSlot>();
                _writer.Put(orders.Count);
                foreach (var slot in orders)
                {
                    _writer.Put((int)slot.type);
                    _writer.Put(slot.amount);
                    _writer.Put(slot.isPallet);
                }
            }
        }

        private static void WritePrices()
        {
            var pm    = SingletonBehaviour<PriceManager>.Instance;
            var types = (ProductType[])System.Enum.GetValues(typeof(ProductType));
            _writer.Put(types.Length);
            foreach (var t in types)
            {
                _writer.Put((int)t);
                _writer.Put(pm != null ? pm.GetUnitPrice(t) : 0f);
            }
        }

        private static void WriteLicenses()
        {
            // Replay the mod's own record of every PurchaseLicense call made this session.
            // LicenseTracker is populated by LicensePatches and by ApplyLicensePurchased.
            var all = LicenseTracker.All;
            _writer.Put(all.Count);
            foreach (var (level, group) in all)
            {
                _writer.Put(level);
                _writer.Put(group);
            }
        }

        private static void WriteGrowthState()
        {
            // GrowthTracker records every PurchaseGrowth() call so we can replay them.
            _writer.Put(GrowthTracker.Count);
        }

        private static void WriteEmployees()
        {
            var em       = SingletonBehaviour<EmployeeManager>.Instance;
            var hired    = em?.HiredEmployees ?? new List<HiringManager.EmployeeStats>();
            _writer.Put(hired.Count);
            foreach (var s in hired)
            {
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
            }
        }

        private static void WriteStoreOpenState()
        {
            bool isOpen = SingletonBehaviour<OpenCloseLabel>.Instance?.IsOpen ?? false;
            _writer.Put(isOpen);
        }

        private static void WriteFurniturePositions()
        {
            var list = new List<Furniture>();
            foreach (var f in FurnitureRegistry.All)
                if (f != null) list.Add(f);

            _writer.Put(list.Count);
            foreach (var f in list)
            {
                _writer.Put((int)f.Type);
                _writer.Put(f.FurnitureID);
                _writer.Put(f.transform.position.x);
                _writer.Put(f.transform.position.y);
                _writer.Put(f.transform.position.z);
                _writer.Put(f.transform.eulerAngles.x);
                _writer.Put(f.transform.eulerAngles.y);
                _writer.Put(f.transform.eulerAngles.z);
            }
        }

        private static void WritePlaceablePositions()
        {
            var list = new List<Placeable>();
            foreach (var p in PlaceableRegistry.All)
                if (p != null) list.Add(p);

            _writer.Put(list.Count);
            foreach (var p in list)
            {
                _writer.Put((int)p.Type);
                _writer.Put(p.PlaceableID);
                _writer.Put(p.transform.position.x);
                _writer.Put(p.transform.position.y);
                _writer.Put(p.transform.position.z);
                _writer.Put(p.transform.eulerAngles.x);
                _writer.Put(p.transform.eulerAngles.y);
                _writer.Put(p.transform.eulerAngles.z);
            }
        }

        private static void WriteOvenStates()
        {
            var cooking = new List<Oven>();
            foreach (var o in OvenRegistry.All)
                if (o != null && o.Cooking) cooking.Add(o);

            _writer.Put(cooking.Count);
            foreach (var o in cooking)
            {
                _writer.Put(o.FurnitureID);
                _writer.Put(OvenRegistry.GetElapsed(o.FurnitureID));
            }
        }

        private static void WriteTrayStates()
        {
            var list = new List<Tray>();
            foreach (var t in TrayRegistry.All)
                if (t != null) list.Add(t);

            _writer.Put(list.Count);
            foreach (var t in list)
            {
                _writer.Put(t.TrayID);
                _writer.Put(t.gameObject.activeSelf);
                _writer.Put(t.transform.position.x);
                _writer.Put(t.transform.position.y);
                _writer.Put(t.transform.position.z);
                _writer.Put(t.transform.eulerAngles.x);
                _writer.Put(t.transform.eulerAngles.y);
                _writer.Put(t.transform.eulerAngles.z);
            }
        }

        private static void WriteVehiclePositions()
        {
            var list = new List<VehichleInteractable>();
            foreach (var v in VehicleRegistry.All)
                if (v != null) list.Add(v);

            _writer.Put(list.Count);
            foreach (var v in list)
            {
                _writer.Put((int)v.VehicleType);
                _writer.Put(v.VehicleID);
                _writer.Put(v.transform.position.x);
                _writer.Put(v.transform.position.y);
                _writer.Put(v.transform.position.z);
                _writer.Put(v.transform.eulerAngles.x);
                _writer.Put(v.transform.eulerAngles.y);
                _writer.Put(v.transform.eulerAngles.z);
            }
        }

        // ── Apply (client side) ──────────────────────────────────────────────────

        public static void Apply(NetDataReader r)
        {
            if (SingletonBehaviour<StockManager>.Instance == null)
            {
                _pendingBytes = r.GetRemainingBytes();
                Plugin.Log.LogInfo("[Net] StateSnapshot buffered — game not loaded yet. Load your save to sync.");
                return;
            }
            ApplyInternal(r);
        }

        public static void ApplyPendingIfAny()
        {
            if (_pendingBytes == null) return;
            Plugin.Log.LogInfo("[Net] Applying buffered StateSnapshot now that game is loaded.");
            var r = new NetDataReader(_pendingBytes);
            _pendingBytes = null;
            ApplyInternal(r);
        }

        public static bool HasPending => _pendingBytes != null;

        private static void ApplyInternal(NetDataReader r)
        {
            ApplyMoney(r);
            ApplyShelves(r);
            ApplyBoxes(r);
            ApplyOrders(r);
            ApplyPrices(r);
            ApplyLicenses(r);
            ApplyGrowthState(r);
            ApplyEmployees(r);
            ApplyStoreOpenState(r);
            ApplyFurniturePositions(r);
            ApplyPlaceablePositions(r);
            ApplyOvenStates(r);
            ApplyTrayStates(r);
            ApplyVehiclePositions(r);
            ApplyPallets(r);
            NpcNetworkManager.ApplyFromSnapshot(r);
            ApplyOfferState(r);
            ApplyTimeOfDay(r);
            ApplyStoreName(r);
            ApplyExperience(r);
            ApplyVendingMachines(r);
            ApplyDecoration(r);
            ApplyCheckoutState(r);
            Plugin.Log.LogInfo("[Net] StateSnapshot applied.");
        }

        private static void ApplyMoney(NetDataReader r)
        {
            float balance = r.GetFloat();
            var em = SingletonBehaviour<EconomyManager>.Instance;
            if (em == null) return;
            Traverse.Create(em).Field("softCurrency").SetValue(balance);
            Traverse.Create(em).Method("SaveSoftCurrency").GetValue();
        }

        private static void ApplyShelves(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                string shelfId     = r.GetString();
                int    productType = r.GetInt();
                int    newCount    = r.GetInt();
                SyncShelf(shelfId, productType, newCount);
            }
        }

        private static void ApplyBoxes(NetDataReader r)
        {
            int count = r.GetInt();
            var boxes = GetSpawnedBoxes();
            for (int i = 0; i < count; i++)
            {
                int  boxId    = r.GetInt();
                bool isStored = r.GetBool(); // NEW: false = box is held by someone
                var  pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                if (boxes != null && boxes.TryGetValue(boxId, out var box) && box != null)
                {
                    box.transform.position    = pos;
                    box.transform.eulerAngles = euler;
                    // Carried boxes: hide until the carrier sends BoxDropped/BoxThrown.
                    if (!isStored) box.gameObject.SetActive(false);
                }
            }
        }

        private static void ApplyOrders(NetDataReader r)
        {
            int areaCount = r.GetInt();
            var om = SingletonBehaviour<OrderManager>.Instance;
            for (int i = 0; i < areaCount; i++)
            {
                int orderCount = r.GetInt();
                if (orderCount == 0) continue;
                var slots = new List<BuyPanel.CartSlot>();
                for (int j = 0; j < orderCount; j++)
                    slots.Add(new BuyPanel.CartSlot((ProductType)r.GetInt(), r.GetInt(), r.GetBool()));
                if (om == null) continue;
                var area = (OrderManager.OrderReceivingArea)i;
                var existing = om.GetOrders(area);
                if (existing.Count == 0)
                    NetApply.Run(() => om.AddOrder(slots, area));
            }
        }

        private static void ApplyPrices(NetDataReader r)
        {
            int count = r.GetInt();
            var pm    = SingletonBehaviour<PriceManager>.Instance;
            for (int i = 0; i < count; i++)
            {
                var   t     = (ProductType)r.GetInt();
                float price = r.GetFloat();
                if (pm != null && price > 0f)
                    NetApply.Run(() =>
                    {
                        pm.SetUnitPrice(t, price);
                        // Fire the event so shelf price labels refresh visually.
                        EventManager.NotifyEvent(ProductEvents.PRODUCT_PRICE_CHANGED, t, price);
                    });
            }
        }

        private static void ApplyLicenses(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                int level = r.GetInt();
                int group = r.GetInt();
                LicenseTracker.Record(level, group);
                NetApply.Run(() => ProductLicenseManager.PurchaseLicense(level, (ProductGroup)group));
            }
        }

        private static void ApplyGrowthState(NetDataReader r)
        {
            int count = r.GetInt();
            var gm    = SingletonBehaviour<GrowthManager>.Instance;
            if (gm == null) { for (int i = 0; i < count; i++) GrowthTracker.Record(); return; }
            for (int i = 0; i < count; i++)
            {
                GrowthTracker.Record();
                NetApply.Run(() => gm.PurchaseGrowth());
            }
        }

        private static void ApplyEmployees(NetDataReader r)
        {
            int count = r.GetInt();
            var em    = SingletonBehaviour<EmployeeManager>.Instance;
            for (int i = 0; i < count; i++)
            {
                int    empId   = r.GetInt();
                string name    = r.GetString();
                var    role    = (EmployeeRole)r.GetInt();
                int    move    = r.GetInt();
                int    work    = r.GetInt();
                int    wage    = r.GetInt();
                bool   early   = r.GetBool();
                int    deskId  = r.GetInt();
                bool   whouse  = r.GetBool();
                var    dept    = (ProductGroup)r.GetInt();

                if (em == null) continue;
                // Skip employees already present (save file may have loaded them).
                if (em.HiredEmployees.Find(e => e.employeeID == empId).employeeID == empId) continue;

                var s = new HiringManager.EmployeeStats(name, role, move, work, wage, deskId);
                s.employeeID       = empId;
                s.isEarylyShift    = early;
                s.checkoutDeskID   = deskId;
                s.includeWarehouse = whouse;
                s.department       = dept;
                NetApply.Run(() => em.HireEmployee(s));
            }
        }

        private static void ApplyStoreOpenState(NetDataReader r)
        {
            bool isOpen = r.GetBool();
            var  label  = SingletonBehaviour<OpenCloseLabel>.Instance;
            if (label != null && label.IsOpen != isOpen)
                NetApply.Run(() => label.OnLabelClicked());
        }

        private static void ApplyFurniturePositions(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                var  type  = (FurnitureType)r.GetInt();
                int  id    = r.GetInt();
                var  pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  euler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  f     = FurnitureRegistry.Get(type, id);
                if (f != null)
                    NetApply.Run(() =>
                    {
                        f.transform.position    = pos;
                        f.transform.eulerAngles = euler;
                        f.SavePosition();
                    });
            }
        }

        private static void ApplyPlaceablePositions(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                var type  = (PlaceableType)r.GetInt();
                int id    = r.GetInt();
                var pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var euler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var p     = PlaceableRegistry.Get(type, id);
                if (p != null)
                    NetApply.Run(() =>
                    {
                        p.transform.position    = pos;
                        p.transform.eulerAngles = euler;
                        p.SavePosition();
                    });
            }
        }

        private static void ApplyOvenStates(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                int   ovenId  = r.GetInt();
                float elapsed = r.GetFloat();
                var   oven    = OvenRegistry.Get(ovenId);
                if (oven == null || oven.Cooking) continue;
                var ovenRef = oven;
                NetApply.Run(() =>
                {
                    ovenRef.TurnOn();
                    if (elapsed > 0.5f)
                        AdvanceOvenCooking(ovenRef, elapsed);
                });
            }
        }

        internal static void AdvanceOvenCooking(Oven oven, float elapsed)
        {
            float remaining = Mathf.Max(0.05f, CookableProduct.COOK_DURATION - elapsed);
            var trayShelf = Traverse.Create(oven).Field("trayShelf")
                .GetValue<System.Collections.Generic.List<TrayShelf>>();
            if (trayShelf == null) return;

            foreach (var shelf in trayShelf)
            {
                if (shelf.ContainedTrayID == -1) continue;
                var tray = shelf.ContainedTray;
                if (tray == null || tray.IsCooked || tray.IsEmpty()) continue;

                // Stop the full-duration coroutine TurnOn() just started
                var routine = Traverse.Create(tray).Field("cookingRoutine").GetValue<Coroutine>();
                if (routine != null) ((MonoBehaviour)(object)tray).StopCoroutine(routine);

                // Advance product visuals to the right progress
                var products = Traverse.Create(tray).Field("containedProducts")
                    .GetValue<System.Collections.Generic.List<Product>>();
                if (products != null)
                {
                    foreach (var p in products)
                    {
                        if (p == null) continue;
                        var cp = p as CookableProduct;
                        if (cp == null) continue;
                        Traverse.Create(cp).Field("cookTween").GetValue<DG.Tweening.Tween>()?.Goto(elapsed, true);
                    }
                }

                // Schedule FinishCooking at the correct remaining time
                var trayRef = tray;
                DG.Tweening.DOVirtual.DelayedCall(remaining, () =>
                    NetApply.Run(() => Traverse.Create(trayRef).Method("FinishCooking").GetValue()));
            }
        }

        private static void ApplyTrayStates(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                int  trayId = r.GetInt();
                bool active = r.GetBool();
                var  pos    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  euler  = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  tray   = TrayRegistry.Get(trayId);
                if (tray == null) continue;
                NetApply.Run(() =>
                {
                    tray.transform.position    = pos;
                    tray.transform.eulerAngles = euler;
                    tray.gameObject.SetActive(active);
                });
            }
        }

        private static void ApplyVehiclePositions(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                var  type  = (VehicleType)r.GetInt();
                int  id    = r.GetInt();
                var  pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  euler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var  v     = VehicleRegistry.Get(type, id);
                if (v == null) continue;
                v.transform.position    = pos;
                v.transform.eulerAngles = euler;
            }
        }

        private static void WriteOfferState()
        {
            var om = SingletonBehaviour<OfferManager>.Instance;
            _writer.Put(om != null && om.IsOfferPurchased());
        }

        private static void ApplyOfferState(NetDataReader r)
        {
            bool isActive = r.GetBool();
            if (!isActive) return;
            var om = SingletonBehaviour<OfferManager>.Instance;
            if (om != null) NetApply.Run(() => om.ActivateOffer());
        }

        private static void WriteVendingMachines()
        {
            var list = new System.Collections.Generic.List<VendingPlaceable>();
            foreach (var p in PlaceableRegistry.All)
                if (p is VendingPlaceable vp) list.Add(vp);
            _writer.Put(list.Count);
            foreach (var vp in list)
            {
                _writer.Put((int)vp.Type);
                _writer.Put(vp.PlaceableID);
                _writer.Put(Traverse.Create(vp).Field("storedMoney").GetValue<float>());
            }
        }

        private static void ApplyVendingMachines(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                var  type  = (PlaceableType)r.GetInt();
                int  id    = r.GetInt();
                float money = r.GetFloat();
                var vp = PlaceableRegistry.Get(type, id) as VendingPlaceable;
                if (vp == null) continue;
                Traverse.Create(vp).Field("storedMoney").SetValue(money);
                Traverse.Create(vp).Field("readyUI").GetValue<GameObject>()?.SetActive(money > 0f);
                Traverse.Create(vp).Field("productInfoUI").GetValue<GameObject>()?.SetActive(money <= 0f);
            }
        }

        private static void WriteDecoration()
        {
            var dm = SingletonBehaviour<DecorationManager>.Instance;
            _writer.Put(dm?.UsedFloorDecoration ?? 0);
            _writer.Put(dm?.UsedWallDecoration  ?? 0);
        }

        private static void ApplyDecoration(NetDataReader r)
        {
            int floor = r.GetInt();
            int wall  = r.GetInt();
            var dm = SingletonBehaviour<DecorationManager>.Instance;
            if (dm == null) return;
            NetApply.Run(() =>
            {
                dm.UseDecoration(DecorationUI.DecorationType.FLOOR, floor);
                dm.UseDecoration(DecorationUI.DecorationType.WALL,  wall);
            });
        }

        private static void WriteExperience()
        {
            var em = SingletonBehaviour<ExperienceManager>.Instance;
            _writer.Put(em?.CurrentXP    ?? 0);
            _writer.Put(em?.CurrentLevel ?? 1);
        }

        private static void ApplyExperience(NetDataReader r)
        {
            int xp    = r.GetInt();
            int level = r.GetInt();
            var em = SingletonBehaviour<ExperienceManager>.Instance;
            if (em == null) return;
            var t = Traverse.Create(em);
            t.Field("currentXP").SetValue(xp);
            t.Field("currentLevel").SetValue(level);
            em.CurrentXP    = xp;
            em.CurrentLevel = level;
            t.Method("Repaint").GetValue();
        }

        private static void WriteTimeOfDay()
        {
            var tm = SingletonBehaviour<TimeManager>.Instance;
            float mins     = tm != null ? Traverse.Create(tm).Field("mins").GetValue<float>()     : 0f;
            int   quarters = tm != null ? Traverse.Create(tm).Field("quarters").GetValue<int>()   : 0;
            _writer.Put(mins);
            _writer.Put(quarters);
        }

        private static void ApplyTimeOfDay(NetDataReader r)
        {
            float mins     = r.GetFloat();
            int   quarters = r.GetInt();
            var tm = SingletonBehaviour<TimeManager>.Instance;
            if (tm == null) return;
            Traverse.Create(tm).Field("mins").SetValue(mins);
            Traverse.Create(tm).Field("quarters").SetValue(quarters);
            Traverse.Create(tm).Method("RepaintSun").GetValue();
            Traverse.Create(tm).Method("RepaintHourText").GetValue();
        }

        private static void WriteStoreName()
        {
            string name = GenericDataSerializer.HasKey(StartWindow.supermarketNameKey)
                ? GenericDataSerializer.LoadString(StartWindow.supermarketNameKey)
                : "";
            _writer.Put(name);
        }

        private static void ApplyStoreName(NetDataReader r)
        {
            string name = r.GetString();
            if (string.IsNullOrEmpty(name)) return;
            GenericDataSerializer.SaveString(StartWindow.supermarketNameKey, name);
            var window = Object.FindAnyObjectByType<SupermarketNameWindow>();
            if (window != null)
                NetApply.Run(() =>
                {
                    Traverse.Create(window).Field("currentName").SetValue(name);
                    Traverse.Create(window).Method("ApplyName", name).GetValue();
                });
        }

        private static void WritePallets()
        {
            var pm = SingletonBehaviour<PalletManager>.Instance;
            var dict = pm != null
                ? Traverse.Create(pm).Field("spawnedPallets").GetValue<System.Collections.Generic.Dictionary<int, Pallet>>()
                : null;

            if (dict == null) { _writer.Put(0); return; }

            // Build a null-free list BEFORE writing the count so count == bytes written.
            // Writing dict.Count and then skipping nulls inside the loop would corrupt
            // all snapshot sections that follow pallets in the byte stream.
            var valid = new System.Collections.Generic.List<Pallet>();
            foreach (var kv in dict)
                if (kv.Value != null) valid.Add(kv.Value);

            _writer.Put(valid.Count);
            foreach (var p in valid)
            {
                _writer.Put(p.PalletID);
                _writer.Put(p.transform.position.x);
                _writer.Put(p.transform.position.y);
                _writer.Put(p.transform.position.z);
                _writer.Put(p.transform.eulerAngles.x);
                _writer.Put(p.transform.eulerAngles.y);
                _writer.Put(p.transform.eulerAngles.z);
            }
        }

        private static void ApplyPallets(NetDataReader r)
        {
            int count = r.GetInt();
            var pm    = SingletonBehaviour<PalletManager>.Instance;
            for (int i = 0; i < count; i++)
            {
                int palletId = r.GetInt();
                var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var pallet   = pm?.GetPallet(palletId);
                if (pallet == null) continue;
                pallet.transform.position    = pos;
                pallet.transform.eulerAngles = euler;
            }
        }

        // ── Checkout desk state ──────────────────────────────────────────────────

        private static void WriteCheckoutState()
        {
            var managers = Object.FindObjectsByType<CheckoutManager>(FindObjectsSortMode.None);
            _writer.Put(managers.Length);
            foreach (var mgr in managers)
            {
                _writer.Put(mgr.FurnitureID);

                // Products currently on the belt.
                var placed = Traverse.Create(mgr).Field("productsPlaced").GetValue<System.Collections.Generic.List<Product>>();
                int pCount = placed?.Count ?? 0;
                _writer.Put(pCount);
                if (placed != null)
                    foreach (var p in placed)
                    {
                        _writer.Put((int)p.Data.type);
                        _writer.Put(Traverse.Create(p).Field("tempPrice").GetValue<float>());
                    }

                // How many have already been scanned.
                _writer.Put(Traverse.Create(mgr).Field("scannedProductCount").GetValue<int>());

                // Active customer (try common field names; 0 = no customer).
                Customer active = null;
                foreach (var fname in new[] { "customer", "currentCustomer", "queuedCustomer", "checkoutCustomer", "activeCustomer" })
                {
                    active = Traverse.Create(mgr).Field(fname).GetValue<Customer>();
                    if (active != null) break;
                }
                int netId = active != null ? NpcNetworkManager.GetNetworkId(active) : 0;
                _writer.Put(netId);
            }
        }

        private static void ApplyCheckoutState(NetDataReader r)
        {
            int count = r.GetInt();
            for (int i = 0; i < count; i++)
            {
                int furnitureId = r.GetInt();
                int pCount      = r.GetInt();
                var types       = new int[pCount];
                var prices      = new float[pCount];
                for (int j = 0; j < pCount; j++) { types[j] = r.GetInt(); prices[j] = r.GetFloat(); }
                int scanned      = r.GetInt();
                int customerNetId = r.GetInt();

                // Find the matching desk on the client.
                CheckoutManager mgr = null;
                foreach (var m in Object.FindObjectsByType<CheckoutManager>(FindObjectsSortMode.None))
                    if (m.FurnitureID == furnitureId) { mgr = m; break; }
                if (mgr == null) continue;

                // If there's an active customer, we must restore them first.
                // If the customer pool is exhausted and the proxy can't be found, skip
                // the entire desk — a partial restore (products without a customer ref)
                // causes a null-deref crash when the host later fires OnLeaveStarted.
                if (customerNetId > 0)
                {
                    var customer = NpcNetworkManager.GetClientCustomer(customerNetId);
                    if (customer == null)
                    {
                        Plugin.Log.LogWarning($"[Snapshot] Checkout desk {furnitureId}: customer {customerNetId} not in pool — skipping desk state to avoid null-deref on payment.");
                        continue;
                    }
                    NetApply.Run(() => mgr.GetIntoTheQueue(customer));
                }

                // Restore products on belt.
                if (pCount > 0)
                {
                    var pool     = SingletonBehaviour<ProductPool>.Instance;
                    if (pool == null) continue;
                    var products = new System.Collections.Generic.List<Product>();
                    for (int j = 0; j < pCount; j++)
                    {
                        var p = pool.GetProduct((ProductType)types[j]);
                        Traverse.Create(p).Field("tempPrice").SetValue(prices[j]);
                        products.Add(p);
                    }
                    NetApply.Run(() => mgr.PlaceProducts(products));
                }

                // Restore scan progress by directly setting the count field — avoids
                // re-triggering money events that MoveToBag would normally fire.
                if (scanned > 0)
                {
                    int s = scanned;
                    NetApply.Run(() => Traverse.Create(mgr).Field("scannedProductCount").SetValue(s));
                }
            }
        }

        // ── Shared helpers ───────────────────────────────────────────────────────

        private static void SyncShelf(string shelfId, int productType, int targetCount)
        {
            var shelf = ShelfRegistry.Get(shelfId);
            if (shelf == null) return;
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
                for (int i = 0; i < delta; i++)
                {
                    var p = pool.GetProduct(type);
                    NetApply.Run(() => shelf.PlaceProduct(p));
                }
            else if (delta < 0)
                for (int i = 0; i < -delta; i++)
                {
                    var p = shelf.GetRandomProduct();
                    if (p == null) break;
                    NetApply.Run(() => shelf.RemoveProduct(p));
                    pool.PutBackToPool(p);
                }
        }

        internal static Dictionary<int, Box> GetSpawnedBoxes()
        {
            var bm = SingletonBehaviour<BoxManager>.Instance;
            if (bm == null) return null;
            return Traverse.Create(bm).Field("spawnedBoxes").GetValue<Dictionary<int, Box>>();
        }
    }
}
