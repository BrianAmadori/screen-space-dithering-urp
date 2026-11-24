using UnityEngine;

namespace Brian.Dithering
{
    /// <summary>
    /// Follows one camera's pose from frame to frame and totals how far that motion moved the rendered image on screen,
    /// so the dithering grain can travel with the image. Offsets are in pixels, x right and y up.
    /// Call <see cref="Advance"/> once per rendered frame with the camera's pose and lens.
    /// </summary>
    public sealed class GrainOffsetTracker
    {
        /// <summary>A rotation larger than this between two frames counts as a cut.</summary>
        public const float CutRotationDegrees = 15f;

        /// <summary>A position change longer than this between two frames counts as a cut.</summary>
        public const float CutDistanceMeters = 0.1f;

        /// <summary>How far camera rotation has moved the whole image.</summary>
        public Vector2 rotationOffset { get; private set; }

        /// <summary>
        /// How far camera position changes have moved a point 1 m in front of the camera.
        /// A pixel at depth d moved this much divided by d.
        /// </summary>
        public Vector2 positionOffsetAtOneMeter { get; private set; }

        private bool hasPreviousPose;
        private Vector3 previousPosition;
        private Quaternion previousRotation;

        /// <summary>
        /// Adds the image motion since the previous call. The first call after construction or <see cref="Forget"/>
        /// only remembers the pose. Calling twice with the same pose adds nothing.
        /// </summary>
        /// <param name="position">The camera's world position.</param>
        /// <param name="rotation">The camera's world rotation.</param>
        /// <param name="verticalFovDegrees">The camera's vertical field of view.</param>
        /// <param name="pixelHeight">The height of the render target, in pixels.</param>
        public void Advance(Vector3 position, Quaternion rotation, float verticalFovDegrees, float pixelHeight)
        {
            if (hasPreviousPose)
                AddMotion(position, rotation, verticalFovDegrees, pixelHeight);

            hasPreviousPose = true;
            previousPosition = position;
            previousRotation = rotation;
        }

        /// <summary>Drops the remembered pose. The next <see cref="Advance"/> starts a new baseline and adds nothing.</summary>
        public void Forget()
        {
            hasPreviousPose = false;
        }

        private void AddMotion(Vector3 position, Quaternion rotation, float verticalFovDegrees, float pixelHeight)
        {
            Vector3 step = position - previousPosition;
            bool isCut = GetAngleDegrees(previousRotation, rotation) > CutRotationDegrees || step.magnitude > CutDistanceMeters;

            if (isCut)
            {
                // A cut changes the whole image, so the grain starts over with no position stretch.
                positionOffsetAtOneMeter = Vector2.zero;
                return;
            }

            float focalPixels = pixelHeight * 0.5f / Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
            Quaternion toCamera = Conjugate(rotation);

            // The previous forward direction lands where the old screen center went. Roll leaves it in place.
            Vector3 previousForward = toCamera * (previousRotation * Vector3.forward);
            rotationOffset += new Vector2(previousForward.x, previousForward.y) / previousForward.z * focalPixels;

            // Only the right and up parts of the step move the image. The forward part is a dolly, which keeps the grain still.
            Vector3 localStep = toCamera * step;
            positionOffsetAtOneMeter -= new Vector2(localStep.x, localStep.y) * focalPixels;
        }

        private static float GetAngleDegrees(Quaternion from, Quaternion to)
        {
            float dot = Mathf.Min(Mathf.Abs(Quaternion.Dot(from, to)), 1f);
            return 2f * Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        // Quaternion.Inverse, Euler and AngleAxis call native code and throw outside the Unity runtime.
        // The conjugate of a unit quaternion is its inverse and stays managed.
        private static Quaternion Conjugate(Quaternion rotation)
        {
            return new Quaternion(-rotation.x, -rotation.y, -rotation.z, rotation.w);
        }
    }
}
