import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildRouteFile, toGeoJson, toGpx } from '../RouteFinder/wwwroot/js/route-export.js';

const route = {
    coordinates: [
        { latitude: 56.1572, longitude: 10.2107 },
        { latitude: 56.1672, longitude: 10.2207 },
        { latitude: 56.1572, longitude: 10.2107 },
    ],
    distance: 5.23,
    duration: 3600,
};
const meta = { name: '5.2 km walking loop', profile: 'foot-walking' };

test('GPX has one track point per coordinate, in lat/lon order', () => {
    const gpx = toGpx(route, meta);

    assert.match(gpx, /^<\?xml version="1.0" encoding="UTF-8"\?>/);
    assert.equal(gpx.match(/<trkpt /g).length, 3);
    assert.match(gpx, /<trkpt lat="56.1672" lon="10.2207"\/>/);
    assert.match(gpx, /<name>5.2 km walking loop<\/name>/);
});

test('GPX escapes XML in the route name', () => {
    const gpx = toGpx(route, { ...meta, name: 'Tom & Jerry <loop>' });

    assert.match(gpx, /<name>Tom &amp; Jerry &lt;loop&gt;<\/name>/);
});

test('GeoJSON is a LineString with longitude first', () => {
    const geoJson = JSON.parse(toGeoJson(route, meta));
    const [feature] = geoJson.features;

    assert.equal(geoJson.type, 'FeatureCollection');
    assert.equal(feature.geometry.type, 'LineString');
    assert.deepEqual(feature.geometry.coordinates[1], [10.2207, 56.1672]);
    assert.equal(feature.properties.distanceKm, 5.23);
});

test('buildRouteFile names the file after the distance and picks the MIME type', () => {
    const gpx = buildRouteFile('gpx', route, meta);
    const geoJson = buildRouteFile('geojson', route, meta);

    assert.equal(gpx.fileName, 'route-5.2km.gpx');
    assert.equal(gpx.mimeType, 'application/gpx+xml');
    assert.equal(geoJson.fileName, 'route-5.2km.geojson');
    assert.equal(geoJson.mimeType, 'application/geo+json');
});

test('buildRouteFile rejects unknown formats', () => {
    assert.throws(() => buildRouteFile('kml', route, meta), /Unknown format: kml/);
});
