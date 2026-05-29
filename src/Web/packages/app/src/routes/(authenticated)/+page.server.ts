import type { PageServerLoad } from './$types';
import { transformChartData, type TransformedChartData } from '$lib/utils/chart-data-transform';

// Hours of data for initial fast load (most recent)
const INITIAL_HOURS = 6;
// Total hours to fetch (matches GLUCOSE_CHART_FETCH_HOURS)
const TOTAL_HOURS = 48;
// Server-side timeout for chart data fetches so a slow/hung API can't hold the
// SSR response (and therefore the browser) open indefinitely. The catch blocks
// below degrade gracefully to null on timeout. Composes with the request's own
// abort signal via the API client factory's AbortSignal.any merge.
const CHART_FETCH_TIMEOUT_MS = 10000;

export const load: PageServerLoad = async ({ locals }) => {
	const { apiClient } = locals;

	const now = Date.now();
	const intervalMs = 5 * 60 * 1000;

	// Calculate time boundaries
	const endTime = Math.ceil(now / intervalMs) * intervalMs;
	const initialStartTime = endTime - INITIAL_HOURS * 60 * 60 * 1000;
	const fullStartTime = endTime - TOTAL_HOURS * 60 * 60 * 1000;

	// Fetch initial recent data immediately (blocking)
	let initialChartData: TransformedChartData | null = null;
	try {
		const data = await apiClient.chartData.getDashboardChartData(
			initialStartTime,
			endTime,
			5,
			AbortSignal.timeout(CHART_FETCH_TIMEOUT_MS),
		);
		initialChartData = transformChartData(data);
	} catch (err) {
		console.error('Error loading initial chart data:', err);
	}

	// Create a promise for historical data that will stream in
	const historicalDataPromise = (async (): Promise<TransformedChartData | null> => {
		try {
			const data = await apiClient.chartData.getDashboardChartData(
				fullStartTime,
				initialStartTime,
				5,
				AbortSignal.timeout(CHART_FETCH_TIMEOUT_MS),
			);
			return transformChartData(data);
		} catch (err) {
			console.error('Error loading historical chart data:', err);
			return null;
		}
	})();

	return {
		initialChartData,
		streamed: {
			historicalChartData: historicalDataPromise,
		},
	};
};
