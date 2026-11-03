import test from 'node:test'
import assert from 'node:assert/strict'
import definition from '../device-profiles/freematics-one-plus.experimental.json' with { type: 'json' }
import { createCarProfile, translateAttribute } from '../src/lib/carProfiles.js'

test('Freematics engineering values are not decoded twice and display in either unit system', () => {
  const metric = createCarProfile(definition, 'metric')
  const imperial = createCarProfile(definition, 'imperial')
  assert.equal(translateAttribute(metric, 'io266', '300').canonicalValue, 300)
  assert.equal(translateAttribute(metric, 'io270', '-12').canonicalValue, -12)
  assert.equal(translateAttribute(metric, 'io271', '-10').canonicalValue, -10)
  assert.equal(translateAttribute(metric, 'coolantTemp', '90').value, 90)
  assert.equal(translateAttribute(imperial, 'coolantTemp', '90').value, 194)
  assert.equal(translateAttribute(metric, 'obdSpeed', '100').value, 100)
  assert.ok(Math.abs(translateAttribute(imperial, 'obdSpeed', '100').value - 62.1371) < 0.01)
  assert.equal(translateAttribute(metric, 'battery', '13.85').value, 13.85)
  assert.equal(definition.attributeMap.battery.statusCardField, 'power')
})

test('Freematics vector and identifier text survive without scalar conversion', () => {
  const profile = createCarProfile(definition)
  assert.equal(translateAttribute(profile, 'acceleration', '0.12;0.04;-0.25').value, '0.12;0.04;-0.25')
  assert.equal(translateAttribute(profile, 'vin', '00000123456789012').value, '00000123456789012')
  assert.equal(translateAttribute(profile, 'io30', '2').status, 'unmapped')
  assert.equal(translateAttribute(profile, 'dtcs', 'P0420').status, 'unmapped')
})
