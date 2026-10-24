import contextlib
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

import requests
import route_export as r


class RoutingTests(unittest.TestCase):
    def setUp(self):
        # Fixed two-point Calgary fixture; tests must not depend on exported routes.
        self.geometry = 'erpvHt{dwTA?'
        self.start = {'lon': -114.0631, 'lat': 51.0443}
        self.end = {'lon': -114.0703, 'lat': 51.0558}
        self.body = {'routes': [{'geometry': self.geometry,
                                'summary': {'distance': 1776.5, 'duration': 420.9},
                                'segments': [{'steps': [{'instruction': 'Head north', 'type': 11,
                                             'distance': 17.3, 'duration': 10.4, 'way_points': [0, 2]}]}]}]}

    def response(self, body=None, status=200):
        return Mock(status_code=status, json=Mock(return_value=self.body if body is None else body))

    def test_known_polyline(self):
        self.assertEqual(r.decode_polyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@'),
                         [[38.5, -120.2], [40.7, -120.95], [43.252, -126.453]])
        for bad in ('', '_', '\x00', '~~~~~~~~~~~~'):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                r.decode_polyline(bad)

    @patch.object(r.requests, 'post')
    def test_json_and_coordinate_order(self, post):
        post.return_value = self.response()
        data = r.request_route('test-only', self.start, self.end)
        self.assertEqual(data['origin']['longitude'], self.start['lon'])
        self.assertEqual(data['destination']['latitude'], self.end['lat'])
        self.assertEqual(data['distanceMeters'], 1776.5)
        self.assertEqual(data['durationSeconds'], 420.9)
        self.assertEqual(data['steps'][0]['maneuverType'], 11)
        self.assertEqual(data['coordinatesLonLat'][0], [-114.06283, 51.04435])
        self.assertEqual(post.call_args.kwargs['json']['coordinates'], [[-114.0631, 51.0443], [-114.0703, 51.0558]])
        json.dumps(data, allow_nan=False)
        for lon, lat in data['coordinatesLonLat']:
            self.assertTrue(-180 <= lon <= 180 and -90 <= lat <= 90)

    def test_cli_validation(self):
        for args in (['--start-lon', '1'], ['--end-lat', '1'],
                     ['--start-lon', '181', '--start-lat', '0'],
                     ['--end-lon', '0', '--end-lat', '91'],
                     ['--start-lon', 'nan', '--start-lat', '0'],
                     ['--end-lon', '0', '--end-lat', 'inf']):
            with self.subTest(args=args), contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as exc:
                r.parse_args(args)
            self.assertEqual(exc.exception.code, 2)
        args = r.parse_args(['--start-lon', '-114', '--start-lat', '51'])
        self.assertEqual(args.start_point, {'lon': -114, 'lat': 51})
        self.assertEqual(r.parse_args(['--start', 'island']).start_point, r.PRESETS['island'])

    @patch.object(r.requests, 'post')
    def test_api_errors(self, post):
        for status in (400, 401, 403, 406, 429, 500):
            post.return_value = self.response(status=status)
            with self.subTest(status=status), self.assertRaises(RuntimeError):
                r.request_route('test-only', self.start, self.end)
        post.side_effect = requests.Timeout('secret-test-value')
        with self.assertRaises(RuntimeError) as exc:
            r.request_route('test-only', self.start, self.end)
        self.assertNotIn('secret-test-value', str(exc.exception))
        post.side_effect = None
        for body in ({}, {'routes': []}, [], {'routes': [{'geometry': '_'}]}):
            post.return_value = self.response(body)
            with self.assertRaises((RuntimeError, ValueError)):
                r.request_route('test-only', self.start, self.end)
        post.return_value = self.response()
        post.return_value.json.side_effect = ValueError('invalid')
        with self.assertRaises(RuntimeError):
            r.request_route('test-only', self.start, self.end)

    @patch.object(r.requests, 'post')
    def test_missing_summary_and_invalid_geography(self, post):
        self.body['routes'][0]['summary']['distance'] = None
        post.return_value = self.response()
        with self.assertRaises(ValueError):
            r.request_route('test-only', self.start, self.end)
        with self.assertRaises(ValueError):
            r.request_route('test-only', {'lon': 200, 'lat': 51}, self.end)

    @patch.dict(os.environ, {'HEIGIT_API_KEY': 'secret-test-value'})
    @patch.object(r.requests, 'post')
    def test_output_creation_and_safe_failure(self, post):
        post.return_value = self.response()
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'route.json'
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(r.main(['--output', str(path)]), 0)
            data = json.loads(path.read_text())
            self.assertEqual(data['coordinatesLonLat'][0], [-114.06283, 51.04435])
            self.assertNotIn('secret-test-value', path.read_text())
            original = path.read_bytes()
            post.side_effect = requests.ConnectionError('secret-test-value')
            errors = io.StringIO()
            with contextlib.redirect_stderr(errors):
                self.assertEqual(r.main(['--output', str(path)]), 1)
            self.assertEqual(path.read_bytes(), original)
            self.assertNotIn('secret-test-value', errors.getvalue())

    @patch.dict(os.environ, {'HEIGIT_API_KEY': ''})
    @patch.object(r.requests, 'post')
    def test_missing_key(self, post):
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(r.main([]), 1)
        post.assert_not_called()


if __name__ == '__main__':
    unittest.main()
