using System;
using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    public sealed class PlayerWaterDetector : MonoBehaviour
    {
        private readonly HashSet<WaterVolume> overlappingVolumes = new HashSet<WaterVolume>();
        private readonly List<WaterVolume> staleVolumes = new List<WaterVolume>();
        private CharacterController characterController;

        public event Action<bool> WaterStateChanged;
        public bool IsInWater { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void FixedUpdate()
        {
            if (overlappingVolumes.Count == 0 || characterController == null) return;
            staleVolumes.Clear();
            foreach (WaterVolume volume in overlappingVolumes)
                if (volume == null || !volume.Intersects(characterController.bounds)) staleVolumes.Add(volume);
            foreach (WaterVolume volume in staleVolumes) overlappingVolumes.Remove(volume);
            if (staleVolumes.Count > 0) SetWaterState(overlappingVolumes.Count > 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            WaterVolume volume = other.GetComponentInParent<WaterVolume>();
            if (volume == null || !overlappingVolumes.Add(volume)) return;
            SetWaterState(true);
        }

        private void OnTriggerExit(Collider other)
        {
            WaterVolume volume = other.GetComponentInParent<WaterVolume>();
            if (volume == null || !overlappingVolumes.Remove(volume)) return;
            SetWaterState(overlappingVolumes.Count > 0);
        }

        private void OnDisable()
        {
            overlappingVolumes.Clear();
            SetWaterState(false);
        }

        private void SetWaterState(bool inWater)
        {
            if (IsInWater == inWater) return;
            IsInWater = inWater;
            WaterStateChanged?.Invoke(inWater);
        }
    }
}
