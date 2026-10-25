// Builds GPX and GeoJSON files from a route the API has already returned,
// so downloads never need a second trip to the routing service.

/**
 * @typedef {{ latitude: number, longitude: number }} Coordinate
 * @typedef {{ coordinates: Coordinate[], distance: number, duration: number }} Route
 * @typedef {{ name: string, profile: string }} RouteMeta
 */

const formats = {
    gpx: { extension: 'gpx', mimeType: 'application/gpx+xml', build: toGpx },
    geojson: { extension: 'geojson', mimeType: 'application/geo+json', build: toGeoJson },
};

/**
 * @param {'gpx' | 'geojson'} format
 * @param {Route} route
 * @param {RouteMeta} meta
 * @returns {{ fileName: string, mimeType: string, content: string }}
 */
export function buildRouteFile(format, route, meta) {
    const spec = formats[format];
    if (!spec) {
        throw new Error(`Unknown format: ${format}`);
    }

    return {
        fileName: `route-${route.distance.toFixed(1)}km.${spec.extension}`,
        mimeType: spec.mimeType,
        content: spec.build(route, meta),
    };
}

/** @param {Route} route @param {RouteMeta} meta */
export function toGpx(route, meta) {
    const points = route.coordinates
        .map(({ latitude, longitude }) => `      <trkpt lat="${latitude}" lon="${longitude}"/>`)
        .join('\n');

    return `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="Route Finder" xmlns="http://www.topografix.com/GPX/1/1">
  <trk>
    <name>${escapeXml(meta.name)}</name>
    <type>${escapeXml(meta.profile)}</type>
    <trkseg>
${points}
    </trkseg>
  </trk>
</gpx>
`;
}

/** @param {Route} route @param {RouteMeta} meta */
export function toGeoJson(route, meta) {
    const featureCollection = {
        type: 'FeatureCollection',
        features: [
            {
                type: 'Feature',
                properties: {
                    name: meta.name,
                    profile: meta.profile,
                    distanceKm: route.distance,
                    durationSeconds: route.duration,
                },
                geometry: {
                    type: 'LineString',
                    // GeoJSON puts longitude first.
                    coordinates: route.coordinates.map(({ latitude, longitude }) => [longitude, latitude]),
                },
            },
        ],
    };

    return JSON.stringify(featureCollection, null, 2);
}

/** @param {string} value */
function escapeXml(value) {
    return value
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&apos;');
}
