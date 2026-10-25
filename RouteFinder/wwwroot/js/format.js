/** @param {number} lat @param {number} lng */
export function formatCoordinates(lat, lng) {
    return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

/** @param {number} durationSeconds */
export function formatDuration(durationSeconds) {
    const totalMinutes = Math.max(1, Math.round(durationSeconds / 60));
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours === 0) {
        return `${totalMinutes} min`;
    }

    return minutes === 0 ? `${hours} hr` : `${hours} hr ${minutes} min`;
}

/**
 * Reads the most useful message out of an API error response.
 * @param {Response} response
 * @param {string} fallback
 */
export async function readErrorMessage(response, fallback) {
    try {
        const problem = await response.json();
        const firstValidationError = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
        return firstValidationError || problem?.detail || fallback;
    } catch {
        return fallback;
    }
}
