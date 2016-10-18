﻿using System;
using System.Linq;
using App.Metrics.Core;
using App.Metrics.DataProviders;
using App.Metrics.Health;
using App.Metrics.MetricData;
using App.Metrics.Registries;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace App.Metrics.Facts.Core
{
    public class DefaultContextTests
    {
        private static readonly IOptions<AppMetricsOptions> Options = Microsoft.Extensions.Options.Options.Create(new AppMetricsOptions());

        private static readonly IHealthCheckDataProvider HealthCheckDataProvider =
            new DefaultHealthCheckDataProvider(new HealthCheckRegistry(Enumerable.Empty<HealthCheck>(), Options));

        private static readonly IMetricsBuilder MetricsBuilder = new DefaultMetricsBuilder(Options.Value.SystemClock);
        private static readonly Func<IMetricsRegistry> MetricsRegistry = () => new DefaultMetricsRegistry();
        private static readonly IMetricsDataProvider MetricsDataProvider = 
           new DefaultMetricsDataProvider(Options.Value.SystemClock, Enumerable.Empty<EnvironmentInfoEntry>());

        private readonly IMetricsContext _context = new MetricsContext(Options.Value.GlobalContextName,
            Options.Value.SystemClock, MetricsRegistry, MetricsBuilder, HealthCheckDataProvider, MetricsDataProvider);

        public Func<IMetricsContext, MetricsData> CurrentData => ctx => _context.Advanced.MetricsDataProvider.GetMetricsData(ctx);

        [Fact]
        public void MetricsContext_CanCreateSubcontext()
        {
            _context.Context("test").Counter("counter", Unit.Requests);

            var counterValue = CurrentData(_context).ChildMetrics.SelectMany(c => c.Counters).Single();

            counterValue.Name.Should().Be("counter");
        }

        [Fact]
        public void MetricsContext_CanPropagateValueTags()
        {
            _context.Counter("test", Unit.None, "tag");
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Single().Tags.Should().Equal("tag");

            _context.Meter("test", Unit.None, tags: "tag");
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Meters.Single().Tags.Should().Equal("tag");

            _context.Histogram("test", Unit.None, tags: "tag");
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Histograms.Single().Tags.Should().Equal("tag");

            _context.Timer("test", Unit.None, tags: "tag");
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Timers.Single().Tags.Should().Equal("tag");
        }

        [Fact]
        public void MetricsContext_ChildWithSameNameAreSameInstance()
        {
            var first = _context.Context("test");
            var second = _context.Context("test");

            ReferenceEquals(first, second).Should().BeTrue();
        }

        [Fact]
        public void MetricsContext_DataProviderReflectsChildContxts()
        {
            var counter = _context
                .Context("test")
                .Counter("test", Unit.Bytes);

            counter.Increment();

            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).ChildMetrics.Should().HaveCount(1);
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).ChildMetrics.Single().Counters.Should().HaveCount(1);
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).ChildMetrics.Single().Counters.Single().Value.Count.Should().Be(1);

            counter.Increment();

            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).ChildMetrics.Single().Counters.Single().Value.Count.Should().Be(2);
        }

        [Fact]
        public void MetricsContext_DataProviderReflectsNewMetrics()
        {
            _context.Counter("test", Unit.Bytes).Increment();

            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Should().HaveCount(1);
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Single().Name.Should().Be("test");
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Single().Value.Count.Should().Be(1L);
        }

        [Fact]
        public void MetricsContext_DisabledChildContextDoesNotShowInData()
        {
            _context.Context("test").Counter("test", Unit.Bytes).Increment();

            CurrentData(_context).ChildMetrics.Single()
                .Counters.Single().Name.Should().Be("test");

            _context.ShutdownContext("test");

            CurrentData(_context).ChildMetrics.Should().BeEmpty();
        }

        [Fact]
        public void MetricsContext_DowsNotThrowOnMetricsOfDifferentTypeWithSameName()
        {
            ((Action)(() =>
            {
                var name = "Test";
                _context.Gauge(name, () => 0.0, Unit.Calls);
                _context.Counter(name, Unit.Calls);
                _context.Meter(name, Unit.Calls);
                _context.Histogram(name, Unit.Calls);
                _context.Timer(name, Unit.Calls);
            })).ShouldNotThrow();
        }

        [Fact]
        public void MetricsContext_EmptyChildContextIsSameContext()
        {
            var child = _context.Context(string.Empty);
            ReferenceEquals(_context, child).Should().BeTrue();
            child = _context.Context(null);
            ReferenceEquals(_context, child).Should().BeTrue();
        }

        [Fact]
        public void MetricsContext_MetricsAddedAreVisibleInTheDataProvider()
        {
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Should().BeEmpty();
            _context.Counter("test", Unit.Bytes);
            _context.Advanced.MetricsDataProvider.GetMetricsData(_context).Counters.Should().HaveCount(1);
        }

        [Fact]
        public void MetricsContext_MetricsArePresentInMetricsData()
        {
            var counter = _context.Counter("test", Unit.Requests);

            counter.Increment();

            var counterValue = CurrentData(_context).Counters.Single();

            counterValue.Name.Should().Be("test");
            counterValue.Unit.Should().Be(Unit.Requests);
            counterValue.Value.Count.Should().Be(1);
        }

        [Fact]
        public void MetricsContext_RaisesShutdownEventOnDispose()
        {
            //TODO: AH - FluentAssertions no longer has MonitorEvents

            //_context.MonitorEvents();
            //_context.Dispose();
            //_context.ShouldRaise("ContextShuttingDown");
        }

        [Fact]
        public void MetricsContext_RaisesShutdownEventOnMetricsDisable()
        {
            //TODO: AH - FluentAssertions no longer has MonitorEvents
            //context.MonitorEvents();
            //context.Advanced.CompletelyDisableMetrics();
            //context.ShouldRaise("ContextShuttingDown");
        }
    }
}