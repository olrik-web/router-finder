import { test } from 'node:test';
import assert from 'node:assert/strict';
import { formatDuration, readErrorMessage } from '../RouteFinder/wwwroot/js/format.js';

test('formatDuration rounds to minutes and shows hours when needed', () => {
    assert.equal(formatDuration(20), '1 min');
    assert.equal(formatDuration(45 * 60), '45 min');
    assert.equal(formatDuration(2 * 3600), '2 hr');
    assert.equal(formatDuration(3600 + 5 * 60), '1 hr 5 min');
});

test('readErrorMessage prefers the first validation error', async () => {
    const response = Response.json({ detail: 'One or more validation errors occurred.', errors: { Distance: ['Distance must be between 1 and 50 km.'] } }, { status: 400 });

    assert.equal(await readErrorMessage(response, 'fallback'), 'Distance must be between 1 and 50 km.');
});

test('readErrorMessage uses the problem detail', async () => {
    const response = Response.json({ detail: 'Too many routes requested. Try again in 42 seconds.' }, { status: 429 });

    assert.equal(await readErrorMessage(response, 'fallback'), 'Too many routes requested. Try again in 42 seconds.');
});

test('readErrorMessage falls back when the body is not JSON', async () => {
    const response = new Response('Bad gateway', { status: 502 });

    assert.equal(await readErrorMessage(response, 'fallback'), 'fallback');
});
