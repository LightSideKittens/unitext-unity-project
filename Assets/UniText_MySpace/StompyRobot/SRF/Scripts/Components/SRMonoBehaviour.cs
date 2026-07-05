namespace SRF
{
    using System.Diagnostics;
    using UnityEngine;

        public abstract class SRMonoBehaviour : MonoBehaviour
    {
                public Transform CachedTransform
        {
            [DebuggerStepThrough]
            [DebuggerNonUserCode]
            get
            {
                if (_transform == null)
                {
                    _transform = base.transform;
                }

                return _transform;
            }
        }

                public GameObject CachedGameObject
        {
            [DebuggerStepThrough]
            [DebuggerNonUserCode]
            get
            {
                if (_gameObject == null)
                {
                    _gameObject = base.gameObject;
                }

                return _gameObject;
            }
        }

        // Override existing getters for legacy usage

        // ReSharper disable InconsistentNaming
        public new Transform transform
        {
            get { return CachedTransform; }
        }

#if !UNITY_5 && !UNITY_2017_1_OR_NEWER

		public new Collider collider
		{
			get { return CachedCollider; }
		}
		public new Collider2D collider2D
		{
			get { return CachedCollider2D; }
		}
		public new Rigidbody rigidbody
		{
			get { return CachedRigidBody; }
		}
		public new Rigidbody2D rigidbody2D
		{
			get { return CachedRigidBody2D; }
		}
		public new GameObject gameObject
		{
			get { return CachedGameObject; }
		}

#endif

        // ReSharper restore InconsistentNaming
        
        private Transform _transform;
        private GameObject _gameObject;

                /// <param name="value">Object to check</param>
        /// <param name="fieldName">Debug name to pass in</param>
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void AssertNotNull(object value, string fieldName = null)
        {
            SRDebugUtil.AssertNotNull(value, fieldName, this);
        }

        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void Assert(bool condition, string message = null)
        {
            SRDebugUtil.Assert(condition, message, this);
        }

                /// <param name="value">Object to check</param>
        /// <param name="fieldName">Debug name to pass in</param>
        /// <returns>True if object is not null</returns>
        [Conditional("UNITY_EDITOR")]
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void EditorAssertNotNull(object value, string fieldName = null)
        {
            AssertNotNull(value, fieldName);
        }

        [Conditional("UNITY_EDITOR")]
        [DebuggerNonUserCode]
        [DebuggerStepThrough]
        protected void EditorAssert(bool condition, string message = null)
        {
            Assert(condition, message);
        }
    }
}
