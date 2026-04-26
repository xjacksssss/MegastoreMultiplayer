using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

namespace MegastoreMultiplayer.Network
{
    public static class RemotePlayerManager
    {
        private static readonly Dictionary<int, GameObject>  _players        = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, Transform>   _carryAnchors   = new Dictionary<int, Transform>();
        private static readonly Dictionary<int, Box>         _carriedBoxes   = new Dictionary<int, Box>();
        private static readonly Dictionary<int, Transform>   _nametags       = new Dictionary<int, Transform>();
        private static readonly Dictionary<int, Vector3>     _targetPositions = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, Quaternion>  _targetRotations = new Dictionary<int, Quaternion>();

        private const float LerpSpeed = 12f;

        // Set when the client's OnConnectedToHost fires so peerId=-1 maps to the host avatar.
        public static int HostPeerId = -1;

        public static void AddPlayer(NetPeer peer)
        {
            if (_players.ContainsKey(peer.Id)) return;

            // ── Capsule body ──────────────────────────────────────────────────
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"RemotePlayer_{peer.Id}";
            go.layer = 0;
            Object.Destroy(go.GetComponent<CapsuleCollider>());
            go.GetComponent<Renderer>().material.color = new Color(0.2f, 0.6f, 1f);

            // ── Nametag ───────────────────────────────────────────────────────
            var tagGo = new GameObject("Nametag");
            tagGo.transform.SetParent(go.transform);
            tagGo.transform.localPosition = Vector3.up * 1.4f;
            tagGo.transform.localScale    = Vector3.one * 0.15f;
            var tm = tagGo.AddComponent<TextMesh>();
            tm.text      = $"Player {peer.Id + 1}";
            tm.alignment = TextAlignment.Center;
            tm.anchor    = TextAnchor.MiddleCenter;
            tm.fontSize  = 48;
            tm.color     = Color.white;

            // ── Carry anchor (where the held box attaches) ────────────────────
            // Positioned at roughly chest height, in front of the avatar.
            var anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(go.transform);
            anchor.transform.localPosition = new Vector3(0f, 0.15f, 0.6f);
            anchor.transform.localRotation = Quaternion.identity;

            Object.DontDestroyOnLoad(go);
            _players[peer.Id]          = go;
            _carryAnchors[peer.Id]     = anchor.transform;
            _nametags[peer.Id]         = tagGo.transform;
            _targetPositions[peer.Id]  = go.transform.position;
            _targetRotations[peer.Id]  = go.transform.rotation;

            Plugin.Log.LogInfo($"[RemotePlayer] Spawned avatar for peer {peer.Id}.");
        }

        public static void RemovePlayer(NetPeer peer)
        {
            DetachBox(peer.Id);
            if (!_players.TryGetValue(peer.Id, out var go)) return;
            if (go != null) Object.Destroy(go);
            _players.Remove(peer.Id);
            _carryAnchors.Remove(peer.Id);
            _nametags.Remove(peer.Id);
            _targetPositions.Remove(peer.Id);
            _targetRotations.Remove(peer.Id);
            Plugin.Log.LogInfo($"[RemotePlayer] Removed avatar for peer {peer.Id}.");
        }

        public static void SetName(int peerId, string name)
        {
            if (!_players.TryGetValue(peerId, out var go) || go == null) return;
            var tm = go.GetComponentInChildren<TextMesh>();
            if (tm != null) tm.text = name;
        }

        public static void UpdatePosition(int peerId, Vector3 position, Vector3 eulerAngles)
        {
            if (!_players.ContainsKey(peerId)) return;
            _targetPositions[peerId] = position;
            // Only apply yaw (Y) to the capsule body — pitch (X) doesn't make sense
            // on a capsule and causes it to tilt forward/back visually.
            _targetRotations[peerId] = Quaternion.Euler(0f, eulerAngles.y, 0f);
        }

        // Called every frame from Plugin.Update() — lerps avatars and billboards nametags.
        public static void Tick(float dt)
        {
            var cam = Camera.main;
            foreach (var kv in _players)
            {
                var go = kv.Value;
                if (go == null) continue;

                if (_targetPositions.TryGetValue(kv.Key, out var tp))
                    go.transform.position = Vector3.Lerp(go.transform.position, tp, LerpSpeed * dt);

                if (_targetRotations.TryGetValue(kv.Key, out var tr))
                    go.transform.rotation = Quaternion.Slerp(go.transform.rotation, tr, LerpSpeed * dt);

                if (cam != null && _nametags.TryGetValue(kv.Key, out var tag) && tag != null)
                    tag.rotation = cam.transform.rotation;
            }
        }

        // Parent the actual Box GameObject to this avatar's carry anchor.
        public static void AttachBox(int peerId, Box box)
        {
            if (box == null) return;
            if (!_carryAnchors.TryGetValue(peerId, out var anchor) || anchor == null) return;

            DetachBox(peerId); // release any previously held box first

            box.gameObject.SetActive(true);
            box.RigidBody.isKinematic = true;
            box.RigidBody.constraints = RigidbodyConstraints.FreezeAll;
            box.transform.SetParent(anchor);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;

            _carriedBoxes[peerId] = box;
        }

        // Returns the peerId carrying the given box, or -1 if none.
        public static int FindCarrier(int boxId)
        {
            foreach (var kv in _carriedBoxes)
                if (kv.Value != null && kv.Value.BoxID == boxId) return kv.Key;
            return -1;
        }

        // Unparent the held box and restore physics. Caller sets final position.
        public static Box DetachBox(int peerId)
        {
            if (!_carriedBoxes.TryGetValue(peerId, out var box)) return null;
            _carriedBoxes.Remove(peerId);

            if (box == null) return null;
            box.transform.SetParent(null);
            box.RigidBody.constraints = RigidbodyConstraints.None;
            box.RigidBody.isKinematic = false;
            return box;
        }

        public static void RemoveAll()
        {
            foreach (var kv in _carriedBoxes)
                if (kv.Value != null)
                {
                    kv.Value.transform.SetParent(null);
                    kv.Value.RigidBody.constraints = RigidbodyConstraints.None;
                    kv.Value.RigidBody.isKinematic = false;
                }
            _carriedBoxes.Clear();

            foreach (var go in _players.Values)
                if (go != null) Object.Destroy(go);
            _players.Clear();
            _carryAnchors.Clear();
            _nametags.Clear();
            _targetPositions.Clear();
            _targetRotations.Clear();
            HostPeerId = -1;
        }
    }
}
