using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game.Input;
using Game.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Seety.Systems
{
    /// <summary>
    /// Carries the game's real Tool / Cancel action into Seety's UI, so the key that backs out of
    /// a tool also closes Seety's windows.
    ///
    /// Ported from the same system in ADHD gone wild rather than reinvented; the notes below are
    /// what that one learned the hard way and they apply here unchanged.
    ///
    /// The "Default Tool UI" action is contextual and is not emitted for a component appended to
    /// the global Game surface, and the game blocks mouse Tool actions while the pointer is over
    /// UI - so WasPerformedThisFrame cannot close a panel. What survives both is the binding
    /// itself: read the buttons the player has actually bound, without enabling or consuming the
    /// blocked tool action.
    ///
    /// <para>
    /// The buttons come from <see cref="ProxyAction.bindings"/>, which is public and already
    /// carries the player's overrides in <see cref="ProxyBinding.path"/> - so somebody who put
    /// Cancel on the middle mouse button gets that, with nothing to configure in Seety.
    /// </para>
    ///
    /// <para>
    /// Only mouse buttons are followed here. Escape and the gamepad Back button already reach the
    /// UI through the action consumer on the React side, and a second route for them would close
    /// two things per press.
    /// </para>
    /// </summary>
    public partial class CancelKeyUISystem : UISystemBase
    {
        private const string Group = "seety";

        private ProxyAction _cancelAction;
        private ValueBinding<int> _revisionBinding;
        private int _revision;
        private bool _lookupFailureLogged;

        /// <summary>The mouse buttons Cancel is on right now, resolved once rather than per frame.</summary>
        private readonly List<ButtonControl> _buttons = new List<ButtonControl>();

        /// <summary>
        /// The game's own "the bindings have changed" counter. Rebinding Cancel in the options
        /// moves it, which is the signal to look the buttons up again - without it this would
        /// follow whatever was bound when the city loaded until the next restart.
        /// </summary>
        private int _boundAtActionVersion = -1;

        protected override void OnCreate()
        {
            base.OnCreate();

            _revisionBinding = new ValueBinding<int>(Group, "cancelRevision", 0);
            AddBinding(_revisionBinding);

            FindCancelAction();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (_cancelAction == null)
            {
                FindCancelAction();
                return;
            }

            RefreshButtonsIfBindingsChanged();

            for (var i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].wasPressedThisFrame)
                {
                    _revisionBinding.Update(++_revision);
                    break;
                }
            }
        }

        private void FindCancelAction()
        {
            try
            {
                var input = InputManager.instance;
                if (input == null)
                {
                    return;
                }

                _cancelAction = input.FindAction("Tool", "Cancel");

                if (_cancelAction != null)
                {
                    _lookupFailureLogged = false;
                    _boundAtActionVersion = -1;
                }
            }
            catch (Exception e)
            {
                // Input can be incomplete while the world is coming up. Retry quietly after the
                // first useful line rather than turning a startup state into a log full of spam.
                if (!_lookupFailureLogged)
                {
                    _lookupFailureLogged = true;
                    Mod.Log.Warn("Seety: could not find Tool / Cancel yet: " + e.Message);
                }
            }
        }

        /// <summary>Resolves the bound mouse buttons, and only when something has been rebound.</summary>
        private void RefreshButtonsIfBindingsChanged()
        {
            try
            {
                var input = InputManager.instance;
                if (input == null || input.actionVersion == _boundAtActionVersion)
                {
                    return;
                }

                _boundAtActionVersion = input.actionVersion;
                _buttons.Clear();

                foreach (var binding in _cancelAction.bindings)
                {
                    if (!binding.isMouse || !binding.isSet || string.IsNullOrEmpty(binding.path))
                    {
                        continue;
                    }

                    var button = InputSystem.FindControl(binding.path) as ButtonControl;
                    if (button != null && !_buttons.Contains(button))
                    {
                        _buttons.Add(button);
                    }
                }
            }
            catch (Exception e)
            {
                // A binding that cannot be resolved costs this one route out of a window. Escape
                // still works, so this is a smaller thing than an exception every frame.
                _buttons.Clear();
                Mod.Log.Warn("Seety: could not read the Cancel binding: " + e.Message);
            }
        }
    }
}
