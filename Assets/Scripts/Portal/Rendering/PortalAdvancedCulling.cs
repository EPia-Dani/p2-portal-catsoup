// PortalAdvancedCulling.cs
// Advanced culling system for portal recursion optimization

using UnityEngine;

namespace Portal.Rendering {
	public static class PortalAdvancedCulling {
		// Default culling thresholds (can be overridden)
		private const float DefaultMinCoverageForRecursion = 0.001f; // 0.1% screen coverage
		private const float DefaultMaxDistanceForFullRecursion = 20f; // Beyond this, reduce recursion
		private const float DefaultMaxDistanceForAnyRecursion = 100f; // Beyond this, no recursion
		private const float MinViewAngleDot = 0.3f; // Portal must be at least this visible
		
		// Coverage thresholds for recursion levels (relative to base coverage)
		private static readonly float[] CoverageThresholds = {
			0.01f,  // Level 0: 1% coverage needed
			0.005f, // Level 1: 0.5% coverage needed
			0.002f, // Level 2: 0.2% coverage needed
			0.001f, // Level 3+: 0.1% coverage needed
		};

		/// <summary>
		/// Calculates adaptive recursion level based on distance, coverage, and visibility
		/// </summary>
		public static int CalculateAdaptiveRecursionLevel(
			Camera camera,
			Transform sourcePortal,
			Transform destinationPortal,
			MeshRenderer surfaceRenderer,
			int maxRecursionLimit,
			float maxDistanceForFullRecursion = DefaultMaxDistanceForFullRecursion,
			float maxDistanceForAnyRecursion = DefaultMaxDistanceForAnyRecursion,
			float minCoverageForRecursion = DefaultMinCoverageForRecursion) {
			
			if (!camera || !sourcePortal || !destinationPortal || !surfaceRenderer) {
				return 0;
			}

			// Early exit: check basic visibility
			if (!PortalVisibility.IsVisibleToCamera(camera, surfaceRenderer)) {
				return 0;
			}

			// Calculate distance from camera to portal
			float distance = Vector3.Distance(camera.transform.position, sourcePortal.position);
			
			// Distance-based culling: beyond max distance, no recursion
			if (distance > maxDistanceForAnyRecursion) {
				return 0;
			}

			// Calculate screen coverage
			float coverage = PortalVisibility.GetScreenSpaceCoverage(camera, surfaceRenderer);
			
			// Coverage-based early exit: too small to see recursion
			if (coverage < minCoverageForRecursion) {
				return 0;
			}

			// Calculate view angle (how directly we're looking at the portal)
			Vector3 toPortal = (sourcePortal.position - camera.transform.position).normalized;
			Vector3 cameraForward = camera.transform.forward;
			float viewAngleDot = Vector3.Dot(toPortal, cameraForward);
			
			// Angle-based culling: portal too far from view direction
			if (viewAngleDot < MinViewAngleDot) {
				return 0;
			}

			// Start with base recursion level (from portal angle)
			int baseLevel = PortalRecursionSolver.CalculateMaxRecursionLevel(
				sourcePortal, destinationPortal, maxRecursionLimit);

			// Apply distance-based reduction
			int distanceAdjustedLevel = ApplyDistanceReduction(distance, baseLevel, maxDistanceForFullRecursion, maxDistanceForAnyRecursion);
			
			// Apply coverage-based reduction
			int coverageAdjustedLevel = ApplyCoverageReduction(coverage, distanceAdjustedLevel, minCoverageForRecursion);
			
			// Apply angle-based reduction
			int finalLevel = ApplyAngleReduction(viewAngleDot, coverageAdjustedLevel);

			return Mathf.Clamp(finalLevel, 0, maxRecursionLimit - 1);
		}

		/// <summary>
		/// Checks if a specific recursion level would be visible
		/// </summary>
		public static bool IsRecursionLevelVisible(
			Camera camera,
			Transform sourcePortal,
			Transform destinationPortal,
			int recursionLevel,
			float baseCoverage,
			float minCoverageForRecursion = DefaultMinCoverageForRecursion) {
			
			if (recursionLevel <= 0) return true; // Level 0 is always visible if portal is visible
			
			// Each recursion level needs progressively more coverage to be visible
			float requiredCoverage = recursionLevel < CoverageThresholds.Length 
				? CoverageThresholds[recursionLevel] 
				: Mathf.Max(CoverageThresholds[CoverageThresholds.Length - 1], minCoverageForRecursion);
			
			// Recursion levels also get smaller due to distance compounding
			// Estimate: each level reduces effective coverage by ~30%
			float estimatedCoverage = baseCoverage * Mathf.Pow(0.7f, recursionLevel);
			
			return estimatedCoverage >= requiredCoverage;
		}

		/// <summary>
		/// Performs per-level visibility check and returns the maximum visible level
		/// </summary>
		public static int CalculateVisibleRecursionLevels(
			Camera camera,
			Transform sourcePortal,
			Transform destinationPortal,
			MeshRenderer surfaceRenderer,
			int maxRecursionLimit,
			float minCoverageForRecursion = DefaultMinCoverageForRecursion) {
			
			if (!camera || !sourcePortal || !destinationPortal || !surfaceRenderer) {
				return 0;
			}

			float baseCoverage = PortalVisibility.GetScreenSpaceCoverage(camera, surfaceRenderer);
			if (baseCoverage < minCoverageForRecursion) {
				return 0;
			}

			// Check each recursion level to see if it would be visible
			for (int level = maxRecursionLimit - 1; level >= 0; level--) {
				if (IsRecursionLevelVisible(camera, sourcePortal, destinationPortal, level, baseCoverage, minCoverageForRecursion)) {
					return level;
				}
			}

			return 0;
		}

		/// <summary>
		/// Reduces recursion level based on distance
		/// </summary>
		private static int ApplyDistanceReduction(float distance, int baseLevel, float maxDistanceForFullRecursion, float maxDistanceForAnyRecursion) {
			if (distance <= maxDistanceForFullRecursion) {
				return baseLevel; // Full recursion at close range
			}

			// Linear reduction from maxDistanceForFullRecursion to maxDistanceForAnyRecursion
			float t = Mathf.InverseLerp(maxDistanceForFullRecursion, maxDistanceForAnyRecursion, distance);
			int reduction = Mathf.RoundToInt(baseLevel * t);
			
			return Mathf.Max(0, baseLevel - reduction);
		}

		/// <summary>
		/// Reduces recursion level based on screen coverage
		/// </summary>
		private static int ApplyCoverageReduction(float coverage, int baseLevel, float minCoverageForRecursion) {
			if (baseLevel <= 0) return 0;

			// Small portals don't need deep recursion
			for (int i = CoverageThresholds.Length - 1; i >= 0; i--) {
				float threshold = Mathf.Max(CoverageThresholds[i], minCoverageForRecursion);
				if (coverage >= threshold) {
					return Mathf.Min(i + 1, baseLevel);
				}
			}

			// Coverage too small for any recursion
			return 0;
		}

		/// <summary>
		/// Reduces recursion level based on view angle
		/// </summary>
		private static int ApplyAngleReduction(float viewAngleDot, int baseLevel) {
			// When viewing portal at extreme angles, reduce recursion
			// viewAngleDot ranges from -1 (behind) to 1 (directly facing)
			if (viewAngleDot < 0.5f) {
				// Portal is at an angle, reduce recursion
				float reductionFactor = Mathf.InverseLerp(0.3f, 0.5f, viewAngleDot);
				return Mathf.Max(0, Mathf.RoundToInt(baseLevel * reductionFactor));
			}

			return baseLevel;
		}

		/// <summary>
		/// Calculates the effective distance for recursion (accounts for portal size)
		/// </summary>
		public static float GetEffectiveDistance(Camera camera, Transform portal, MeshRenderer surfaceRenderer) {
			if (!camera || !portal || !surfaceRenderer) return float.MaxValue;

			float distance = Vector3.Distance(camera.transform.position, portal.position);
			
			// Adjust distance based on portal size (larger portals are "closer" for culling purposes)
			Bounds bounds = surfaceRenderer.bounds;
			float portalSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
			float adjustedDistance = distance / Mathf.Max(portalSize, 0.1f);
			
			return adjustedDistance;
		}

		/// <summary>
		/// Checks if portal pair should render at all (early exit optimization)
		/// </summary>
		public static bool ShouldRenderPortalPair(
			Camera camera,
			Transform sourcePortal,
			Transform destinationPortal,
			MeshRenderer surfaceRenderer,
			float maxDistanceForAnyRecursion = DefaultMaxDistanceForAnyRecursion,
			float minCoverageForRecursion = DefaultMinCoverageForRecursion) {
			
			if (!camera || !sourcePortal || !destinationPortal || !surfaceRenderer) {
				return false;
			}

			// Basic visibility check
			if (!PortalVisibility.IsVisibleToCamera(camera, surfaceRenderer)) {
				return false;
			}

			// Distance check
			float distance = Vector3.Distance(camera.transform.position, sourcePortal.position);
			if (distance > maxDistanceForAnyRecursion) {
				return false;
			}

			// Coverage check
			float coverage = PortalVisibility.GetScreenSpaceCoverage(camera, surfaceRenderer);
			if (coverage < minCoverageForRecursion) {
				return false;
			}

			return true;
		}
	}
}

