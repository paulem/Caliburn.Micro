﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Caliburn.Micro.Tests.Core
{
    public class ConductorWithCollectionOneActiveTests
    {
        private class StateScreen : Screen
        {
            public bool IsClosed { get; private set; }
            public bool IsClosable { get; set; }

            public override void CanClose(Action<bool> callback)
            {
                callback(IsClosable);
            }

            protected override void OnDeactivate(bool close)
            {
                base.OnDeactivate(close);
                IsClosed = close;
            }
        }

        private class DeferredCloseScreen : StateScreen
        {
            private Action<bool> _closeCallback;

            public override void CanClose(Action<bool> callback)
            {
                _closeCallback = callback;
            }

            public override Task TryCloseAsync(bool? dialogResult = null)
            {
                _closeCallback?.Invoke(IsClosable);

                return Task.CompletedTask;
            }
        }

        [Fact]
        public void AddedItemAppearsInChildren()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            Assert.Contains(conducted, conductor.GetChildren());
        }

        [Fact]
        public async Task CanCloseIsTrueWhenItemsAreClosable()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new StateScreen
            {
                IsClosable = true
            };
            conductor.Items.Add(conducted);
            await ((IActivate)conductor).ActivateAsync(CancellationToken.None);
            conductor.CanClose(Assert.True);
            Assert.False(conducted.IsClosed);
        }

        [Fact(Skip = "Investigating close issue. http://caliburnmicro.codeplex.com/discussions/275824")]
        public async Task CanCloseIsTrueWhenItemsAreNotClosableAndCloseStrategyCloses()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive
            {
                CloseStrategy = new DefaultCloseStrategy<IScreen>(true)
            };
            var conducted = new StateScreen
            {
                IsClosable = true
            };
            conductor.Items.Add(conducted);
            await((IActivate)conductor).ActivateAsync(CancellationToken.None);
            conductor.CanClose(Assert.True);
            Assert.True(conducted.IsClosed);
        }

        [Fact]
        public async Task ChildrenAreActivatedIfConductorIsActive()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            await ((IActivate)conductor).ActivateAsync(CancellationToken.None);
            await conductor.ActivateItemAsync(conducted);
            Assert.True(conducted.IsActive);
            Assert.Equal(conducted, conductor.ActiveItem);
        }

        [Fact(Skip = "ActiveItem currently set regardless of IsActive value. See http://caliburnmicro.codeplex.com/discussions/276375")]
        public async Task ChildrenAreNotActivatedIfConductorIsNotActive()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            await ((IActivate)conductor).ActivateAsync(CancellationToken.None);
            Assert.False(conducted.IsActive);
            Assert.NotEqual(conducted, conductor.ActiveItem);
        }

        [Fact(Skip = "Behavior currently allowed; under investigation. See http://caliburnmicro.codeplex.com/discussions/276373")]
        public void ConductorCannotConductSelf()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            Assert.Throws<InvalidOperationException>(() => conductor.Items.Add(conductor));
        }

        [Fact]
        public void ParentItemIsSetOnAddedConductedItem()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            Assert.Equal(conductor, conducted.Parent);
        }

        [Fact]
        public void ParentItemIsSetOnReplacedConductedItem()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var originalConducted = new Screen();
            conductor.Items.Add(originalConducted);
            var newConducted = new Screen();
            conductor.Items[0] = newConducted;
            Assert.Equal(conductor, newConducted.Parent);
        }

        [Fact(Skip = "This is not possible as we don't get the removed items in the event handler.")]
        public void ParentItemIsUnsetOnClear()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            conductor.Items.Clear();
            Assert.NotEqual(conductor, conducted.Parent);
        }

        [Fact]
        public void ParentItemIsUnsetOnRemovedConductedItem()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            conductor.Items.RemoveAt(0);
            Assert.NotEqual(conductor, conducted.Parent);
        }

        [Fact]
        public void ParentItemIsUnsetOnReplaceConductedItem()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = new Screen();
            conductor.Items.Add(conducted);
            var conducted2 = new Screen();
            conductor.Items[0] = conducted2;
            Assert.NotEqual(conductor, conducted.Parent);
            Assert.Equal(conductor, conducted2.Parent);
        }

        [Fact] // See http://caliburnmicro.codeplex.com/discussions/430917
        public async Task TryCloseStressTest()
        {
            var conductor = new Conductor<IScreen>.Collection.OneActive();
            var conducted = Enumerable.Range(0, 10000)
                .Select(i => new Screen
                {
                    DisplayName = i.ToString(CultureInfo.InvariantCulture)
                });
            conductor.Items.AddRange(conducted);

            var defered1 = new DeferredCloseScreen
            {
                DisplayName = "d1",
                IsClosable = true
            };
            var defered2 = new DeferredCloseScreen
            {
                DisplayName = "d2",
                IsClosable = true
            };
            conductor.Items.Insert(0, defered1);
            conductor.Items.Insert(500, defered2);

            var finished = false;
            conductor.CanClose(canClose =>
            {
                finished = true;
                Assert.True(canClose);
            });
            Assert.False(finished);

            await defered1.TryCloseAsync();
            await defered2.TryCloseAsync();
            Assert.True(finished);
        }
    }
}