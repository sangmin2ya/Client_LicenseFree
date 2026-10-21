using NUnit.Framework;

public class RttEstimatorTests
{
	private const long ServerAhead = 1000;

	// 클라이언트 시각 t에 보내고 rtt 뒤에 받은 핑. 서버는 왕복의 절반 지점에서 시각을 찍는다.
	private static bool Ping(RttEstimator estimator, long t, long rtt)
	{
		return estimator.AddSample(t, t + rtt, t + rtt / 2 + ServerAhead);
	}

	[Test]
	public void FirstSample_IsAccepted_EvenWhenRttIsHigh()
	{
		var estimator = new RttEstimator();

		Assert.IsTrue(Ping(estimator, 0, 200));
		Assert.AreEqual(200, estimator.SmoothedRtt, 0.001);
		Assert.AreEqual(ServerAhead, estimator.ClockOffset);
	}

	[Test]
	public void SingleSpike_IsExcluded()
	{
		var estimator = new RttEstimator();
		long t = 0;
		for (int i = 0; i < 20; i++, t += 1000)
		{
			Ping(estimator, t, 50);
		}

		Assert.IsFalse(Ping(estimator, t, 800));
		Assert.AreEqual(50, estimator.SmoothedRtt, 1.0);
		Assert.IsTrue(Ping(estimator, t + 1000, 52));
	}

	[Test]
	public void SustainedRttChange_IsFollowed()
	{
		var estimator = new RttEstimator();
		long t = 0;
		for (int i = 0; i < 20; i++, t += 1000)
		{
			Ping(estimator, t, 50);
		}
		for (int i = 0; i < 30; i++, t += 1000)
		{
			Ping(estimator, t, 200);
		}

		Assert.AreEqual(200, estimator.SmoothedRtt, 15.0);
	}

	[Test]
	public void ClockOffset_PrefersLowestRttSample()
	{
		var estimator = new RttEstimator(outlierMarginMs: 1000);

		// 응답만 늦게 온 비대칭 샘플은 오프셋 오차가 크다
		estimator.AddSample(0, 50, 25 + ServerAhead);
		estimator.AddSample(1000, 1300, 1025 + ServerAhead);

		Assert.AreEqual(ServerAhead, estimator.ClockOffset);
	}

	[Test]
	public void ThirdConsecutiveOutlier_IsAccepted()
	{
		var estimator = new RttEstimator();
		long t = 0;
		for (int i = 0; i < 20; i++, t += 1000)
		{
			Ping(estimator, t, 50);
		}

		Assert.IsFalse(Ping(estimator, t, 800));
		Assert.IsFalse(Ping(estimator, t + 1000, 800));
		Assert.IsTrue(Ping(estimator, t + 2000, 800));
	}

	[Test]
	public void ClockOffset_UsesOnlyLastEightSamples()
	{
		var estimator = new RttEstimator();

		// RTT가 가장 낮지만 오프셋이 다른 첫 샘플
		estimator.AddSample(0, 10, 5 + ServerAhead + 500);
		long t = 1000;
		for (int i = 0; i < 7; i++, t += 1000)
		{
			Ping(estimator, t, 50);
		}
		Assert.AreEqual(ServerAhead + 500, estimator.ClockOffset);

		// 8개를 넘으면 가장 오래된 샘플이 빠진다
		Ping(estimator, t, 50);
		Assert.AreEqual(ServerAhead, estimator.ClockOffset);
	}

	[Test]
	public void NegativeRtt_IsRejected()
	{
		var estimator = new RttEstimator();

		Assert.IsFalse(estimator.AddSample(100, 50, 0));
		Assert.IsFalse(estimator.HasSample);
	}
}
