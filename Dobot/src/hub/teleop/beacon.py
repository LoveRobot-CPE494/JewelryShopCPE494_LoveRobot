"""Robot beacon: the Robot PC announces its Teleop address on UDP (ADR-0023, ADR-0024, ADR-0027)."""

import ipaddress
import logging
import socket
import threading
from collections.abc import Sequence

logger = logging.getLogger("hub.teleop")

DISCOVERY_PORT = 7778  # the Unity side's HostAddressConfig default
INTERVAL_S = 1.0
_ROBOT_MAGIC = b"JSRB"  # the staff beacon is "JSHB"; the role is the magic
_VERSION = 1


def encode_beacon(address: str, port: int) -> bytes:
    """Return the 11 beacon bytes: magic, version 1, IPv4 in network order, port big-endian."""
    packed = ipaddress.IPv4Address(address).packed
    return _ROBOT_MAGIC + bytes([_VERSION]) + packed + port.to_bytes(2, "big")


def choose_address(local_ips: Sequence[str], mg400_ip: str) -> str | None:
    """Pick the LAN IPv4 to announce: one the MG400 is not on.

    Skips loopback and link-local. An address in the MG400's /24 is the arm's cable, so it loses
    to any other; it is used only when it is the sole choice.
    """
    # shortcut: assumes the arm's network is a /24, upgrade if the cell uses another mask
    arm_net = ipaddress.ip_network(f"{mg400_ip}/24", strict=False)
    addresses = [ipaddress.IPv4Address(a) for a in local_ips]
    usable = [a for a in addresses if not (a.is_loopback or a.is_link_local)]
    off_arm = [a for a in usable if a not in arm_net]
    chosen = (off_arm or usable or [None])[0]
    return None if chosen is None else str(chosen)


def local_ipv4s() -> list[str]:
    """Return this PC's IPv4 addresses, from the hostname lookup."""
    infos = socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)
    return list(dict.fromkeys(str(info[4][0]) for info in infos))


def _broadcast_forever(stop: threading.Event, sock: socket.socket, data: bytes, port: int) -> None:
    with sock:
        while not stop.is_set():
            try:
                sock.sendto(data, ("255.255.255.255", port))
            except OSError as error:
                logger.warning("[beacon] not sent: %s", error)
            stop.wait(INTERVAL_S)


def start_beacon(
    teleop_port: int,
    mg400_ip: str,
    address: str | None = None,
    discovery_port: int = DISCOVERY_PORT,
) -> threading.Event | None:
    """Broadcast the robot beacon about once a second; set the returned Event to stop it.

    Returns None, with a warning, when no usable address exists (the Quest keeps its typed one).
    """
    address = address or choose_address(local_ipv4s(), mg400_ip)
    if address is None:
        logger.warning("[beacon] no LAN IPv4 found; not broadcasting. Pass --beacon-ip.")
        return None
    data = encode_beacon(address, teleop_port)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    # Bound to the announced address so the broadcast leaves on that card, not the MG400's.
    # A --beacon-ip that is not this PC's fails here, at start-up.
    sock.bind((address, 0))
    stop = threading.Event()
    args = (stop, sock, data, discovery_port)
    threading.Thread(target=_broadcast_forever, args=args, daemon=True).start()
    logger.info("[beacon] announcing %s:%d on UDP %d", address, teleop_port, discovery_port)
    return stop
