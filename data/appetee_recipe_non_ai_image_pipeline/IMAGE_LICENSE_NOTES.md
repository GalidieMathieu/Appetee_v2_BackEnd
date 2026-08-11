# Image licensing notes

The acquisition script deliberately limits automatic acceptance to Wikimedia
Commons images whose structured metadata reports a common reusable license:

- Public domain
- CC0
- CC BY
- CC BY-SA

Openverse can be useful for discovery, but Openverse itself warns that it does
not guarantee the accuracy of license metadata. For that reason this first
automated implementation uses Wikimedia Commons structured image metadata as
the acceptance gate.

For any image that will be publicly shipped with Appetee, retain the generated
`image-metadata.json` record and comply with the applicable attribution/share-
alike obligations.
