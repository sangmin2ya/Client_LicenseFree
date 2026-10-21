using System;

// RTT와 서버 시계 오프셋 추정기. Unity 의존성이 없는 순수 C#이라 EditMode 테스트로 검증한다.
// - RTT: RFC 6298의 SRTT/RTTVAR. 첫 샘플로 초기화하고,
//   |rtt - srtt| > 4 * rttvar + 여유값 인 샘플은 이상치로 제외한다.
//   이상치가 연속으로 들어오면 회선 상태가 바뀐 것으로 보고 수용해 새 값을 따라간다.
// - 오프셋: 최근 샘플 중 RTT가 가장 낮은 샘플의 오프셋을 쓴다(NTP 방식).
//   RTT가 낮을수록 왕복 경로의 비대칭 오차가 작기 때문이다.
public class RttEstimator
{
	private const double Alpha = 0.125;
	private const double Beta = 0.25;

	private readonly long _outlierMarginMs;
	private readonly int _acceptAfterOutliers;
	private readonly long[] _windowRtt;
	private readonly long[] _windowOffset;
	private int _windowCount;
	private int _windowNext;
	private int _consecutiveOutliers;

	public bool HasSample { get; private set; }
	public double SmoothedRtt { get; private set; }
	public double RttVariance { get; private set; }
	// 서버 시각 - 클라이언트 시각 (ms)
	public long ClockOffset { get; private set; }

	public RttEstimator(long outlierMarginMs = 50, int acceptAfterOutliers = 3, int offsetWindowSize = 8)
	{
		if (offsetWindowSize < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(offsetWindowSize));
		}
		_outlierMarginMs = outlierMarginMs;
		_acceptAfterOutliers = acceptAfterOutliers;
		_windowRtt = new long[offsetWindowSize];
		_windowOffset = new long[offsetWindowSize];
	}

	// sentAt, receivedAt: 클라이언트 시각(ms), serverTime: 응답에 담긴 서버 시각(ms)
	// 샘플이 반영되면 true, 이상치로 제외되면 false
	public bool AddSample(long sentAt, long receivedAt, long serverTime)
	{
		long rtt = receivedAt - sentAt;
		if (rtt < 0)
		{
			return false;
		}

		if (!HasSample)
		{
			SmoothedRtt = rtt;
			RttVariance = rtt / 2.0;
			HasSample = true;
		}
		else
		{
			double error = rtt - SmoothedRtt;
			bool isOutlier = Math.Abs(error) > 4 * RttVariance + _outlierMarginMs;
			if (isOutlier && ++_consecutiveOutliers < _acceptAfterOutliers)
			{
				return false;
			}
			_consecutiveOutliers = 0;
			RttVariance = (1 - Beta) * RttVariance + Beta * Math.Abs(error);
			SmoothedRtt = (1 - Alpha) * SmoothedRtt + Alpha * rtt;
		}

		// 요청이 서버에 도착한 시점을 왕복의 절반으로 가정
		long offset = serverTime - (sentAt + rtt / 2);
		_windowRtt[_windowNext] = rtt;
		_windowOffset[_windowNext] = offset;
		_windowNext = (_windowNext + 1) % _windowRtt.Length;
		_windowCount = Math.Min(_windowCount + 1, _windowRtt.Length);

		long bestRtt = long.MaxValue;
		for (int i = 0; i < _windowCount; i++)
		{
			if (_windowRtt[i] < bestRtt)
			{
				bestRtt = _windowRtt[i];
				ClockOffset = _windowOffset[i];
			}
		}
		return true;
	}
}
