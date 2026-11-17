from collections.abc import Iterator

import pytest

from tests.fakes.mg400 import FakeMG400


@pytest.fixture
def fake_mg400() -> Iterator[FakeMG400]:
    fake = FakeMG400()
    yield fake
    fake.close()
