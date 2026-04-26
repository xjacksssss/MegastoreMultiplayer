using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    // Syncs customer and employee NPCs so clients see the real game models.
    //
    // CUSTOMERS
    //   Host: assigns sequential networkIds, broadcasts NpcSpawn/Position/Despawn.
    //   Client: suppresses its own CustomerManager simulation. On NpcSpawn, grabs a
    //           pooled Customer GameObject (real model, correct textures) via
    //           CustomerManager.GetRandomAvailableCustomer() and makes it visible
    //           without running AI. NpcPosition packets move it to match the host.
    //
    // EMPLOYEES (Baker, Cashier, Restocker, Unloader, SeafoodStaff)
    //   Host: uses employee.GetEmployeeID() as the stable network ID (same value
    //         on all machines since it comes from saved HiringManager.EmployeeStats).
    //         Broadcasts EmployeeActivated/Position/Deactivated.
    //   Client: blocks AI activation. On EmployeeActivated, looks up the employee
    //           in EmployeeManager's typed dictionaries and makes it visible without AI.
    public static class NpcNetworkManager
    {
        // ── Customer tracking ────────────────────────────────────────────────────

        private static readonly Dictionary<Customer, int> _hostCustomers    = new Dictionary<Customer, int>();
        private static readonly Dictionary<int, Customer> _clientCustomers  = new Dictionary<int, Customer>();
        // Track movement state so we only trigger animation on state change.
        private static readonly Dictionary<int, bool>     _customerMoving   = new Dictionary<int, bool>();
        // Last animation trigger per networkId — written into StateSnapshot so joining clients
        // get the correct pose rather than the default idle.
        private static readonly Dictionary<int, string>   _customerLastAnim = new Dictionary<int, string>();
        private static int _nextNetworkId = 1;

        // ── Employee tracking ────────────────────────────────────────────────────

        private static readonly Dictionary<int, Employee> _hostEmployees    = new Dictionary<int, Employee>();
        private static readonly Dictionary<int, Employee> _clientEmployees  = new Dictionary<int, Employee>();
        private static readonly Dictionary<int, bool>     _employeeMoving   = new Dictionary<int, bool>();

        // ── Wanderer tracking ─────────────────────────────────────────────────────

        // poolIdx → character (host), poolIdx → character (client)
        private static readonly Dictionary<int, CharacterMovement> _hostWanderers       = new Dictionary<int, CharacterMovement>();
        private static readonly Dictionary<int, Vector3>           _hostWandererTargets = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, CharacterMovement> _clientWanderers     = new Dictionary<int, CharacterMovement>();

        // ── Truck tracking ────────────────────────────────────────────────────────

        private static readonly System.Collections.Generic.HashSet<int> _activeTruckAreas =
            new System.Collections.Generic.HashSet<int>();

        public static void RegisterTruck(int areaIndex)   => _activeTruckAreas.Add(areaIndex);
        public static void UnregisterTruck(int areaIndex) => _activeTruckAreas.Remove(areaIndex);

        // ── Car tracking ─────────────────────────────────────────────────────────

        // parkingLotIndex → CustomerCar (both host and client use the same dict type)
        private static readonly Dictionary<int, CustomerCar> _hostCars    = new Dictionary<int, CustomerCar>();
        private static readonly Dictionary<int, CustomerCar> _clientCars  = new Dictionary<int, CustomerCar>();

        // ── Client-side interpolation targets ────────────────────────────────────
        // NPC position packets arrive at 10 Hz.  Storing targets and lerping each
        // frame eliminates the step-teleport jitter seen at low update rates.

        private static readonly Dictionary<int, Vector3>    _customerTargetPos  = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Quaternion> _customerTargetRot  = new Dictionary<int, Quaternion>();
        private static readonly Dictionary<int, Vector3>    _employeeTargetPos  = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Quaternion> _employeeTargetRot  = new Dictionary<int, Quaternion>();
        private static readonly Dictionary<int, Vector3>    _carTargetPos       = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Quaternion> _carTargetRot       = new Dictionary<int, Quaternion>();

        // Last animator state hash broadcast per employee — avoids redundant packets.
        private static readonly Dictionary<int, int> _employeeLastAnimHash = new Dictionary<int, int>();

        // Dedicated writer for the Tick anim loop — kept separate from NetMessages._writer
        // so the anim broadcast never races with Dispatch reusing the shared writer.
        private static readonly LiteNetLib.Utils.NetDataWriter _animWriter = new LiteNetLib.Utils.NetDataWriter();

        // Dead-reckoning: velocity + timestamp per NPC, used to extrapolate position
        // between 10 Hz position packets so movement looks smooth at any frame rate.
        private static readonly Dictionary<int, Vector3> _customerVelocity       = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, float>   _customerLastUpdateTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, Vector3> _employeeVelocity       = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, float>   _employeeLastUpdateTime = new Dictionary<int, float>();
        // Maximum seconds to extrapolate before clamping — avoids runaway drift.
        private const float MaxExtrapolation = 0.3f;

        private const float NpcLerpSpeed = 10f;

        // ── Shared ───────────────────────────────────────────────────────────────

        private static float _broadcastTimer;
        private const float BroadcastInterval = 0.1f; // 10 Hz

        // ── Host — customer API ──────────────────────────────────────────────────

        public static void RegisterCustomer(Customer customer)
        {
            if (_hostCustomers.ContainsKey(customer)) return;
            _hostCustomers[customer] = _nextNetworkId++;
        }

        public static int GetNetworkId(Customer customer)
        {
            _hostCustomers.TryGetValue(customer, out int id);
            return id;
        }

        public static void UnregisterCustomer(Customer customer)
        {
            _hostCustomers.Remove(customer);
        }

        // ── Host — wanderer API ──────────────────────────────────────────────────

        public static void RegisterWanderer(CharacterMovement character, int poolIdx, Vector3 targetPos)
        {
            if (_hostWanderers.ContainsKey(poolIdx)) return;
            _hostWanderers[poolIdx]       = character;
            _hostWandererTargets[poolIdx] = targetPos;
        }

        public static void UnregisterWanderer(int poolIdx)
        {
            _hostWanderers.Remove(poolIdx);
            _hostWandererTargets.Remove(poolIdx);
        }

        public static int GetWandererPoolIndex(CharacterMovement character)
        {
            foreach (var kv in _hostWanderers)
                if (kv.Value == character) return kv.Key;
            return -1;
        }

        // ── Client — wanderer API ─────────────────────────────────────────────────

        public static void SpawnWanderer(int poolIdx, Vector3 startPos, Vector3 startEuler, Vector3 targetPos)
        {
            if (_clientWanderers.ContainsKey(poolIdx)) return;
            var wpm = SingletonBehaviour<WanderingPeopleManager>.Instance;
            if (wpm == null) return;
            var people = Traverse.Create(wpm).Field("wanderingPeople").GetValue<System.Collections.Generic.List<CharacterMovement>>();
            if (people == null || poolIdx >= people.Count) return;
            var character = people[poolIdx];
            if (character == null) return;
            character.transform.position    = startPos;
            character.transform.eulerAngles = startEuler;
            character.gameObject.SetActive(true);
            character.Animator.SetTrigger("walk0");
            character.MoveTo(targetPos, () => DespawnWanderer(poolIdx), 0f, false);
            _clientWanderers[poolIdx] = character;
        }

        public static void DespawnWanderer(int poolIdx)
        {
            if (!_clientWanderers.TryGetValue(poolIdx, out var character)) return;
            _clientWanderers.Remove(poolIdx);
            if (character == null) return;
            character.StopMoving();
            var wpm = SingletonBehaviour<WanderingPeopleManager>.Instance;
            if (wpm != null)
            {
                var pool = Traverse.Create(wpm).Field("poolTransform").GetValue<Transform>();
                if (pool != null) character.transform.position = pool.position;
            }
            character.gameObject.SetActive(false);
        }

        // ── Host — car API ───────────────────────────────────────────────────────

        public static void RegisterCar(CustomerCar car, int parkingLotIndex)
        {
            _hostCars[parkingLotIndex] = car;
        }

        public static void UnregisterCar(int parkingLotIndex)
        {
            _hostCars.Remove(parkingLotIndex);
        }

        // ── Host — employee API ──────────────────────────────────────────────────

        public static void RegisterEmployee(Employee employee)
        {
            int id = employee.GetEmployeeID();
            if (id == 0 || _hostEmployees.ContainsKey(id)) return;
            _hostEmployees[id] = employee;
        }

        public static void UnregisterEmployee(Employee employee)
        {
            _hostEmployees.Remove(employee.GetEmployeeID());
        }

        // ── Client-side interpolation tick ───────────────────────────────────────

        public static void ClientTick(float dt)
        {
            if (!MultiplayerManager.IsClient) return;

            float now = Time.time;

            foreach (var kv in _clientCustomers)
            {
                if (kv.Value == null) continue;
                if (!_customerTargetPos.TryGetValue(kv.Key, out var tp)) continue;

                // Dead-reckoning: advance the target along last known velocity while
                // we wait for the next 10 Hz packet, clamped to MaxExtrapolation seconds.
                if (_customerLastUpdateTime.TryGetValue(kv.Key, out float lastT) &&
                    _customerVelocity.TryGetValue(kv.Key, out var vel))
                {
                    float age = Mathf.Min(now - lastT, MaxExtrapolation);
                    tp += vel * age;
                }

                kv.Value.transform.position = Vector3.Lerp(kv.Value.transform.position, tp, NpcLerpSpeed * dt);
                if (_customerTargetRot.TryGetValue(kv.Key, out var tr))
                    kv.Value.transform.rotation = Quaternion.Slerp(kv.Value.transform.rotation, tr, NpcLerpSpeed * dt);
            }

            foreach (var kv in _clientEmployees)
            {
                if (kv.Value == null) continue;
                if (!_employeeTargetPos.TryGetValue(kv.Key, out var tp)) continue;

                if (_employeeLastUpdateTime.TryGetValue(kv.Key, out float lastT) &&
                    _employeeVelocity.TryGetValue(kv.Key, out var vel))
                {
                    float age = Mathf.Min(now - lastT, MaxExtrapolation);
                    tp += vel * age;
                }

                kv.Value.transform.position = Vector3.Lerp(kv.Value.transform.position, tp, NpcLerpSpeed * dt);
                if (_employeeTargetRot.TryGetValue(kv.Key, out var tr))
                    kv.Value.transform.rotation = Quaternion.Slerp(kv.Value.transform.rotation, tr, NpcLerpSpeed * dt);
            }

            foreach (var kv in _clientCars)
            {
                if (kv.Value == null) continue;
                if (_carTargetPos.TryGetValue(kv.Key, out var tp))
                    kv.Value.transform.position = Vector3.Lerp(kv.Value.transform.position, tp, NpcLerpSpeed * dt);
                if (_carTargetRot.TryGetValue(kv.Key, out var tr))
                    kv.Value.transform.rotation = Quaternion.Slerp(kv.Value.transform.rotation, tr, NpcLerpSpeed * dt);
            }
        }

        // ── Pre-existing NPC registration ────────────────────────────────────────
        // Called from StateSnapshot.Build() before serialising NPC state.
        // Ensures customers and wanderers that were already active when hosting
        // started (before any Activate patch fired) are included in the snapshot.

        public static void RegisterAllActive()
        {
            if (!MultiplayerManager.IsHost) return;

            var cm = SingletonBehaviour<CustomerManager>.Instance;
            if (cm != null)
            {
                var list = Traverse.Create(cm).Field("customers").GetValue<List<Customer>>();
                if (list != null)
                    foreach (var c in list)
                        if (c != null && c.gameObject.activeSelf && !_hostCustomers.ContainsKey(c))
                            RegisterCustomer(c);
            }

            var wpm = SingletonBehaviour<WanderingPeopleManager>.Instance;
            if (wpm != null)
            {
                var wanderers = Traverse.Create(wpm).Field("wanderingPeople").GetValue<List<CharacterMovement>>();
                if (wanderers != null)
                    for (int i = 0; i < wanderers.Count; i++)
                    {
                        var w = wanderers[i];
                        if (w == null || !w.gameObject.activeSelf || _hostWanderers.ContainsKey(i)) continue;
                        var targetPos = Traverse.Create(w).Field("targetPosition").GetValue<Vector3>();
                        RegisterWanderer(w, i, targetPos);
                    }
            }
        }

        // ── Tick (host only) ─────────────────────────────────────────────────────

        public static void Tick()
        {
            if (!MultiplayerManager.IsHost || !MultiplayerManager.IsRunning) return;
            if (MultiplayerManager.Clients.Count == 0) return;

            _broadcastTimer += Time.deltaTime;
            if (_broadcastTimer < BroadcastInterval) return;
            _broadcastTimer = 0f;

            // Customer positions
            var deadC = new List<Customer>();
            foreach (var kv in _hostCustomers)
                if (kv.Key == null) deadC.Add(kv.Key);
            foreach (var d in deadC) _hostCustomers.Remove(d);

            foreach (var kv in _hostCustomers)
            {
                var w = NetMessages.WriteNpcPosition(kv.Value, kv.Key.transform.position, kv.Key.transform.eulerAngles);
                MultiplayerManager.SendToAllUnreliable(w);
            }

            // Employee positions
            var deadE = new List<int>();
            foreach (var kv in _hostEmployees)
                if (kv.Value == null) deadE.Add(kv.Key);
            foreach (var d in deadE) _hostEmployees.Remove(d);

            foreach (var kv in _hostEmployees)
            {
                var w = NetMessages.WriteEmployeePosition(kv.Key, kv.Value.transform.position, kv.Value.transform.eulerAngles);
                MultiplayerManager.SendToAllUnreliable(w);
            }

            // Car positions
            var deadCar = new List<int>();
            foreach (var kv in _hostCars)
                if (kv.Value == null) deadCar.Add(kv.Key);
            foreach (var d in deadCar) _hostCars.Remove(d);

            foreach (var kv in _hostCars)
            {
                var w = NetMessages.WriteCarPosition(kv.Key, kv.Value.transform.position, kv.Value.transform.eulerAngles);
                MultiplayerManager.SendToAllUnreliable(w);
            }

            // Employee animator state — broadcast when state changes so clients show
            // work/idle animations rather than always walking.
            // Uses _animWriter (not NetMessages._writer) to avoid corrupting any packet
            // that Dispatch might be mid-write on the same thread.
            foreach (var kv in _hostEmployees)
            {
                if (kv.Value == null) continue;
                var anim = kv.Value.GetComponentInChildren<Animator>();
                if (anim == null) continue;
                int hash = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
                if (_employeeLastAnimHash.TryGetValue(kv.Key, out int prev) && prev == hash) continue;
                _employeeLastAnimHash[kv.Key] = hash;
                _animWriter.Reset();
                _animWriter.Put((byte)MegastoreMultiplayer.Messages.MessageType.EmployeeAnim);
                _animWriter.Put(kv.Key);
                _animWriter.Put(hash.ToString());
                MultiplayerManager.SendToAllReliable(_animWriter);
            }
        }

        // ── Client — car API ─────────────────────────────────────────────────────

        public static void SpawnCar(int parkingLotIndex, Vector3 position, Vector3 euler)
        {
            if (_clientCars.ContainsKey(parkingLotIndex)) return;
            var ccm = SingletonBehaviour<CarCustomerManager>.Instance;
            if (ccm == null) return;

            var cars = Traverse.Create(ccm).Field("cars").GetValue<System.Collections.Generic.List<CustomerCar>>();
            CustomerCar car = null;
            if (cars != null)
                foreach (var c in cars)
                    if (c != null && !c.gameObject.activeSelf) { car = c; break; }

            if (car == null) { Plugin.Log.LogWarning($"[NpcNet] No pooled car for lot {parkingLotIndex}."); return; }

            car.gameObject.SetActive(true);
            car.transform.position       = position;
            car.transform.eulerAngles    = euler;
            _clientCars[parkingLotIndex] = car;
            _carTargetPos[parkingLotIndex] = position;
            _carTargetRot[parkingLotIndex] = Quaternion.Euler(euler);
        }

        public static void DespawnCar(int parkingLotIndex)
        {
            if (!_clientCars.TryGetValue(parkingLotIndex, out var car)) return;
            _clientCars.Remove(parkingLotIndex);
            _carTargetPos.Remove(parkingLotIndex);
            _carTargetRot.Remove(parkingLotIndex);
            if (car != null) car.gameObject.SetActive(false);
        }

        public static void ApplyCarPosition(int parkingLotIndex, Vector3 pos, Vector3 euler)
        {
            if (!_clientCars.ContainsKey(parkingLotIndex)) return;
            _carTargetPos[parkingLotIndex] = pos;
            _carTargetRot[parkingLotIndex] = Quaternion.Euler(euler);
        }

        // Returns the pool index of a customer so clients can activate the identical model.
        public static int GetCustomerPoolIndex(Customer customer)
        {
            var cm = SingletonBehaviour<CustomerManager>.Instance;
            if (cm == null) return -1;
            var list = Traverse.Create(cm).Field("customers").GetValue<System.Collections.Generic.List<Customer>>();
            return list?.IndexOf(customer) ?? -1;
        }

        // ── Client — customer API ────────────────────────────────────────────────

        // poolIndex: the index in CustomerManager.customers so every client activates
        // the same model. Falls back to a random available customer if the specific
        // one is already taken (e.g. pool exhaustion edge case).
        public static void SpawnCustomer(int networkId, Vector3 position, int poolIndex = -1)
        {
            if (_clientCustomers.ContainsKey(networkId)) return;
            var cm = SingletonBehaviour<CustomerManager>.Instance;
            if (cm == null) return;

            Customer customer = null;

            if (poolIndex >= 0)
            {
                var list = Traverse.Create(cm).Field("customers").GetValue<System.Collections.Generic.List<Customer>>();
                if (list != null && poolIndex < list.Count)
                {
                    var candidate = list[poolIndex];
                    if (candidate != null && !candidate.gameObject.activeSelf)
                        customer = candidate;
                }
            }

            if (customer == null)
                customer = cm.GetRandomAvailableCustomer();

            if (customer == null)
            {
                Plugin.Log.LogWarning($"[NpcNet] No pooled customer available for id {networkId}.");
                return;
            }

            customer.gameObject.SetActive(true);
            customer.transform.position     = position;
            _clientCustomers[networkId]     = customer;
            _customerTargetPos[networkId]   = position;
            _customerTargetRot[networkId]   = customer.transform.rotation;
        }

        public static void DespawnCustomer(int networkId)
        {
            if (!_clientCustomers.TryGetValue(networkId, out var customer)) return;
            _clientCustomers.Remove(networkId);
            _customerTargetPos.Remove(networkId);
            _customerTargetRot.Remove(networkId);
            _customerMoving.Remove(networkId);
            if (customer != null) customer.gameObject.SetActive(false);
        }

        public static void ApplyPosition(int networkId, Vector3 pos, Vector3 euler)
        {
            if (!_clientCustomers.TryGetValue(networkId, out var customer) || customer == null) return;

            // Compute velocity from position delta since last packet for dead-reckoning.
            _customerTargetPos.TryGetValue(networkId, out var prevPos);
            float now = Time.time;
            if (_customerLastUpdateTime.TryGetValue(networkId, out float lastT))
            {
                float dt = now - lastT;
                if (dt > 0.01f)
                    _customerVelocity[networkId] = (pos - prevPos) / dt;
            }
            _customerLastUpdateTime[networkId] = now;

            // Walk/idle animation detection.
            bool nowMoving = Vector3.Distance(pos, prevPos) > 0.05f;
            bool wasMoving = _customerMoving.TryGetValue(networkId, out bool prevMoving) && prevMoving;
            if (nowMoving != wasMoving)
            {
                _customerMoving[networkId] = nowMoving;
                var anim = customer.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.SetTrigger(nowMoving ? "walk0" : "idle0");
            }

            _customerTargetPos[networkId] = pos;
            _customerTargetRot[networkId] = Quaternion.Euler(0f, euler.y, 0f);
        }

        // ── Client — employee API ────────────────────────────────────────────────

        public static void SpawnEmployee(int employeeId, Vector3 position)
        {
            if (_clientEmployees.ContainsKey(employeeId)) return;

            var em       = SingletonBehaviour<EmployeeManager>.Instance;
            if (em == null) return;

            Employee found = FindEmployeeById(em, employeeId);
            if (found == null)
            {
                Plugin.Log.LogWarning($"[NpcNet] No employee found for id {employeeId}.");
                return;
            }

            found.gameObject.SetActive(true);
            found.transform.position        = position;
            _clientEmployees[employeeId]    = found;
            _employeeTargetPos[employeeId]  = position;
            _employeeTargetRot[employeeId]  = found.transform.rotation;
        }

        public static void DespawnEmployee(int employeeId)
        {
            if (!_clientEmployees.TryGetValue(employeeId, out var employee)) return;
            _clientEmployees.Remove(employeeId);
            _employeeTargetPos.Remove(employeeId);
            _employeeTargetRot.Remove(employeeId);
            _employeeMoving.Remove(employeeId);
            if (employee != null) employee.gameObject.SetActive(false);
        }

        public static void ApplyEmployeePosition(int employeeId, Vector3 pos, Vector3 euler)
        {
            if (!_clientEmployees.TryGetValue(employeeId, out var employee) || employee == null) return;

            _employeeTargetPos.TryGetValue(employeeId, out var prevPos);
            float now = Time.time;
            if (_employeeLastUpdateTime.TryGetValue(employeeId, out float lastT))
            {
                float dt = now - lastT;
                if (dt > 0.01f)
                    _employeeVelocity[employeeId] = (pos - prevPos) / dt;
            }
            _employeeLastUpdateTime[employeeId] = now;

            bool nowMoving = Vector3.Distance(pos, prevPos) > 0.05f;
            bool wasMoving = _employeeMoving.TryGetValue(employeeId, out bool prevMoving) && prevMoving;
            if (nowMoving != wasMoving)
            {
                _employeeMoving[employeeId] = nowMoving;
                var anim = employee.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.SetTrigger(nowMoving ? "walk0" : "idle0");
            }

            _employeeTargetPos[employeeId] = pos;
            _employeeTargetRot[employeeId] = Quaternion.Euler(0f, euler.y, 0f);
        }

        // ── Client — animation ───────────────────────────────────────────────────

        public static Customer GetClientCustomer(int networkId) =>
            _clientCustomers.TryGetValue(networkId, out var c) ? c : null;

        public static void TriggerCustomerAnim(int networkId, string trigger)
        {
            if (!_clientCustomers.TryGetValue(networkId, out var customer) || customer == null) return;
            customer.GetComponentInChildren<Animator>()?.SetTrigger(trigger);
            _customerLastAnim[networkId] = trigger;
        }

        // Applies a host-broadcast animator state to a client-side employee proxy.
        // The trigger string is either a named trigger ("walk0", "idle0") or a state
        // hash encoded as a decimal string (from the Tick broadcast path).
        public static void TriggerEmployeeAnim(int employeeId, string trigger)
        {
            if (!_clientEmployees.TryGetValue(employeeId, out var employee) || employee == null) return;
            var anim = employee.GetComponentInChildren<Animator>();
            if (anim == null) return;
            if (int.TryParse(trigger, out int hash))
                anim.CrossFade(hash, 0.1f); // hash-based cross-fade for smooth transitions
            else
                anim.SetTrigger(trigger);
        }

        // ── StateSnapshot serialisation ──────────────────────────────────────────
        // Called from StateSnapshot.Build/ApplyInternal so NPC live state is included
        // in the buffered snapshot — ensuring it arrives after the game scene is loaded.

        public static void WriteToSnapshot(LiteNetLib.Utils.NetDataWriter w)
        {
            // Customers (networkId + position + last anim trigger + pool index)
            var cm           = SingletonBehaviour<CustomerManager>.Instance;
            var customerList = cm != null
                ? Traverse.Create(cm).Field("customers").GetValue<System.Collections.Generic.List<Customer>>()
                : null;

            int cc = 0;
            foreach (var kv in _hostCustomers) if (kv.Key != null) cc++;
            w.Put(cc);
            foreach (var kv in _hostCustomers)
            {
                if (kv.Key == null) continue;
                w.Put(kv.Value);
                w.Put(kv.Key.transform.position.x); w.Put(kv.Key.transform.position.y); w.Put(kv.Key.transform.position.z);
                _customerLastAnim.TryGetValue(kv.Value, out string lastAnim);
                w.Put(lastAnim ?? "");
                w.Put(customerList?.IndexOf(kv.Key) ?? -1); // pool index so clients use same model
            }

            // Employees
            int ec = 0;
            foreach (var kv in _hostEmployees) if (kv.Value != null) ec++;
            w.Put(ec);
            foreach (var kv in _hostEmployees)
            {
                if (kv.Value == null) continue;
                w.Put(kv.Key);
                w.Put(kv.Value.transform.position.x); w.Put(kv.Value.transform.position.y); w.Put(kv.Value.transform.position.z);
            }

            // Cars
            int crc = 0;
            foreach (var kv in _hostCars) if (kv.Value != null) crc++;
            w.Put(crc);
            foreach (var kv in _hostCars)
            {
                if (kv.Value == null) continue;
                w.Put(kv.Key);
                w.Put(kv.Value.transform.position.x); w.Put(kv.Value.transform.position.y); w.Put(kv.Value.transform.position.z);
                w.Put(kv.Value.transform.eulerAngles.x); w.Put(kv.Value.transform.eulerAngles.y); w.Put(kv.Value.transform.eulerAngles.z);
            }

            // Active truck areas (written before wanderers — must match read order)
            w.Put(_activeTruckAreas.Count);
            foreach (int area in _activeTruckAreas) w.Put(area);

            // Wanderers
            int wc = 0;
            foreach (var kv in _hostWanderers) if (kv.Value != null && kv.Value.gameObject.activeSelf) wc++;
            w.Put(wc);
            foreach (var kv in _hostWanderers)
            {
                if (kv.Value == null || !kv.Value.gameObject.activeSelf) continue;
                w.Put(kv.Key);
                w.Put(kv.Value.transform.position.x);    w.Put(kv.Value.transform.position.y);    w.Put(kv.Value.transform.position.z);
                w.Put(kv.Value.transform.eulerAngles.x); w.Put(kv.Value.transform.eulerAngles.y); w.Put(kv.Value.transform.eulerAngles.z);
                _hostWandererTargets.TryGetValue(kv.Key, out Vector3 target);
                w.Put(target.x); w.Put(target.y); w.Put(target.z);
            }
        }

        public static void ApplyFromSnapshot(LiteNetLib.Utils.NetDataReader r)
        {
            // Customers
            int cc = r.GetInt();
            for (int i = 0; i < cc; i++)
            {
                int    netId    = r.GetInt();
                var    pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                string lastAnim = r.GetString();
                int    poolIdx  = r.GetInt();
                SpawnCustomer(netId, pos, poolIdx);
                if (!string.IsNullOrEmpty(lastAnim))
                    TriggerCustomerAnim(netId, lastAnim);
            }

            // Employees
            int ec = r.GetInt();
            for (int i = 0; i < ec; i++)
            {
                int empId = r.GetInt();
                var pos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                SpawnEmployee(empId, pos);
            }

            // Cars
            int crc = r.GetInt();
            for (int i = 0; i < crc; i++)
            {
                int lotIndex = r.GetInt();
                var pos      = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var euler    = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                SpawnCar(lotIndex, pos, euler);
            }

            // Trucks (before wanderers — matches write order)
            int tc = r.GetInt();
            var tm = SingletonBehaviour<TruckManager>.Instance;
            for (int i = 0; i < tc; i++)
            {
                int areaIdx = r.GetInt();
                var truck   = tm?.GetTruck((OrderManager.OrderReceivingArea)areaIdx);
                if (truck != null) NetApply.Run(() => truck.Activate());
            }

            // Wanderers
            int wc = r.GetInt();
            for (int i = 0; i < wc; i++)
            {
                int poolIdx    = r.GetInt();
                var startPos   = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var startEuler = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                var targetPos  = new Vector3(r.GetFloat(), r.GetFloat(), r.GetFloat());
                SpawnWanderer(poolIdx, startPos, startEuler, targetPos);
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        public static void Clear()
        {
            foreach (var kv in _clientCustomers)
                if (kv.Value != null) kv.Value.gameObject.SetActive(false);
            foreach (var kv in _clientEmployees)
                if (kv.Value != null) kv.Value.gameObject.SetActive(false);

            foreach (var kv in _clientCars)
                if (kv.Value != null) kv.Value.gameObject.SetActive(false);
            foreach (var kv in _clientWanderers)
                if (kv.Value != null) kv.Value.gameObject.SetActive(false);

            _clientCustomers.Clear();
            _clientEmployees.Clear();
            _clientCars.Clear();
            _clientWanderers.Clear();
            _hostCustomers.Clear();
            _hostEmployees.Clear();
            _hostCars.Clear();
            _hostWanderers.Clear();
            _hostWandererTargets.Clear();
            _customerMoving.Clear();
            _customerLastAnim.Clear();
            _customerTargetPos.Clear();
            _customerTargetRot.Clear();
            _employeeMoving.Clear();
            _employeeTargetPos.Clear();
            _employeeTargetRot.Clear();
            _carTargetPos.Clear();
            _carTargetRot.Clear();
            _activeTruckAreas.Clear();
            _employeeLastAnimHash.Clear();
            _customerVelocity.Clear();
            _customerLastUpdateTime.Clear();
            _employeeVelocity.Clear();
            _employeeLastUpdateTime.Clear();
            _nextNetworkId  = 1;
            _broadcastTimer = 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Employee FindEmployeeById(EmployeeManager em, int employeeId)
        {
            var t = Traverse.Create(em);

            var bakers = t.Field("bakerDictionary").GetValue<Dictionary<int, Baker>>();
            if (bakers != null && bakers.TryGetValue(employeeId, out var baker)) return baker;

            var cashiers = t.Field("cashierDictionary").GetValue<Dictionary<int, Cashier>>();
            if (cashiers != null && cashiers.TryGetValue(employeeId, out var cashier)) return cashier;

            var restockers = t.Field("restockerDictionary").GetValue<Dictionary<int, Restocker>>();
            if (restockers != null && restockers.TryGetValue(employeeId, out var restocker)) return restocker;

            var unloaders = t.Field("unloadersDictionary").GetValue<Dictionary<int, Unloader>>();
            if (unloaders != null && unloaders.TryGetValue(employeeId, out var unloader)) return unloader;

            var seafood = t.Field("seafoodStaffDictionary").GetValue<Dictionary<int, SeafoodStaff>>();
            if (seafood != null && seafood.TryGetValue(employeeId, out var sf)) return sf;

            return null;
        }
    }
}
