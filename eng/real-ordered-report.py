"""Exercise the actual ordered reporter and pinned SaaS API against disposable MySQL."""
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import secrets
import socket
import sqlite3
import ssl
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from contextlib import closing
from datetime import datetime, timedelta, timezone

import pymysql
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives.serialization import pkcs12
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID

ROOT = Path(__file__).resolve().parents[1]
TENANT = "ordered-report-ci"
DATABASE = "ordered_report_ci"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise RuntimeError("Unexpected redirect from isolated API")


def certificate(directory):
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "localhost")])
    now = datetime.now(timezone.utc)
    cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name)
            .public_key(key.public_key()).serial_number(x509.random_serial_number())
            .not_valid_before(now - timedelta(minutes=5)).not_valid_after(now + timedelta(days=1))
            .add_extension(x509.BasicConstraints(ca=True, path_length=0), critical=True)
            .add_extension(x509.SubjectAlternativeName([
                x509.DNSName("localhost"), x509.IPAddress(ipaddress.ip_address("127.0.0.1"))
            ]), critical=False)
            .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH]), critical=False)
            .sign(key, hashes.SHA256()))
    password = secrets.token_hex(24)
    pem = directory / "server.pem"
    pem.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    (directory / "server.pfx").write_bytes(pkcs12.serialize_key_and_certificates(
        b"loopback", key, cert, None, serialization.BestAvailableEncryption(password.encode())))
    return pem, password


def multipart(fields, payload):
    boundary = uuid.uuid4().hex
    parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode()
             for key, value in fields.items()]
    parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="ci.zip"\r\n'
              'Content-Type: application/octet-stream\r\n\r\n'.encode(),
              payload, f"\r\n--{boundary}--\r\n".encode()]
    return b"".join(parts), "multipart/form-data; boundary=" + boundary


def main():
    if os.environ.get("GITHUB_ACTIONS") != "true":
        raise RuntimeError("This harness only runs in the isolated GitHub Actions service job")
    port = int(os.environ["ORDERED_REPORT_DB_PORT"])
    user = os.environ["ORDERED_REPORT_DB_USER"]
    db_password = os.environ["ORDERED_REPORT_DB_PASSWORD"]
    if not 1 <= port <= 65535 or any(c in user + db_password for c in ";\"'\r\n"):
        raise ValueError("Invalid disposable database configuration")
    api_dir = ROOT / ".integration" / "api"
    client = ROOT / ".integration" / "client" / "RealReportHarness.dll"

    with tempfile.TemporaryDirectory(prefix="ordered-report-ci-") as temp:
        work = Path(temp)
        pem, pfx_password = certificate(work)
        with socket.socket() as reservation:
            reservation.bind(("127.0.0.1", 0))
            api_port = reservation.getsockname()[1]
        base = f"https://127.0.0.1:{api_port}"
        environment = os.environ.copy()
        environment.update({
            "ASPNETCORE_ENVIRONMENT": "Production",
            "ConnectionStrings__DefaultConnection":
                f"server=127.0.0.1;port={port};database={DATABASE};user={user};password={db_password}",
            "AppSettings__AdminAuthSecretKey": secrets.token_hex(32),
            "AppSettings__FileStoragePath": str(work / "storage"),
            "AppSettings__FileStorageUrl": base + "/File/Download/",
            "ObjectStorage__Provider": "Local",
            "DataProtection__KeyRingPath": str(work / "keys"),
            "Kestrel__Endpoints__Http__Url": base,
            "Kestrel__Endpoints__Http__Certificate__Path": str(work / "server.pfx"),
            "Kestrel__Endpoints__Http__Certificate__Password": pfx_password,
        })
        opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}), NoRedirect(),
            urllib.request.HTTPSHandler(context=ssl.create_default_context(cafile=str(pem))))

        def request(path, method="GET", body=None, token=None, content_type=None):
            headers = {"X-Tenant-Id": TENANT}
            if token:
                headers["Authorization"] = "Bearer " + token
            if body is not None and not isinstance(body, bytes):
                body = json.dumps(body).encode()
                content_type = "application/json"
            if content_type:
                headers["Content-Type"] = content_type
            try:
                response = opener.open(urllib.request.Request(
                    base + path, data=body, headers=headers, method=method), timeout=20)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                data = response.read()
                return response.status, json.loads(data) if data else None

        def success(result):
            status, value = result
            if status != 200 or value["code"] != 200:
                raise AssertionError(f"API request failed: HTTP {status}, body {value}")
            return value["body"]

        def worker(mode, record, outbox="outbox", extra=None):
            command = ["dotnet", str(client), mode, str(work / outbox),
                       base + "/Upgrade/Report", str(pem), str(record)]
            if extra is not None:
                command.append(str(extra))
            subprocess.run(command,
                           check=True, timeout=60)

        def queued(outbox):
            with closing(sqlite3.connect(work / outbox / "ordered-reports.db")) as queue:
                return queue.execute("SELECT id,payload FROM outbox ORDER BY id").fetchall()

        def quarantined(outbox):
            with closing(sqlite3.connect(work / outbox / "ordered-reports.db")) as queue:
                return queue.execute("SELECT id,payload,reason FROM quarantine ORDER BY id").fetchall()

        with (work / "api.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen([str(api_dir / "GeneralSpace.Api")], cwd=api_dir,
                                       env=environment, stdout=log, stderr=subprocess.STDOUT)
            try:
                for _ in range(90):
                    if process.poll() is not None:
                        raise RuntimeError("SaaS API exited before readiness")
                    try:
                        with opener.open(base + "/Health", timeout=2) as response:
                            if response.status == 200:
                                break
                    except urllib.error.URLError:
                        pass
                    time.sleep(1)
                else:
                    raise TimeoutError("SaaS API did not become ready")

                with pymysql.connect(host="127.0.0.1", port=port, user=user, password=db_password,
                                     database=DATABASE, autocommit=True) as db:
                    password_hash = hashlib.sha256(secrets.token_bytes(32)).hexdigest()
                    with db.cursor() as cursor:
                        cursor.execute(
                            "INSERT INTO tb_tenant (id,name,identifier,is_active,create_time,activation_time,"
                            "storage_traffic_quota,used_storage_traffic) VALUES (%s,%s,%s,1,UTC_TIMESTAMP(),"
                            "UTC_TIMESTAMP(),%s,0)", (TENANT, "Ordered report CI", TENANT, 10 * 1024**3))
                        cursor.execute("INSERT INTO tb_admin (name,password,account,tenant_id,is_active) "
                                       "VALUES (%s,%s,%s,%s,1)",
                                       ("Ordered report CI", password_hash, "ordered-admin", TENANT))
                    token = success(request("/User/Login", "POST", {
                        "user": {"account": "ordered-admin", "password": password_hash}, "tenantId": TENANT,
                    }))["accessToken"]
                    success(request("/Product/Query", token=token))
                    assert success(request("/Product/Create", "POST", {"name": "Ordered report CI"}, token)) is True
                    products = success(request("/Product/Query?name=Ordered%20report%20CI", token=token))["items"]
                    assert len(products) == 1
                    product = products[0]["id"]
                    payload = b"ordered-report-test-package"
                    form, kind = multipart({"name": "ci-package", "format": ".zip", "version": "2.0.0",
                                            "appType": "1", "platform": "1", "productId": product,
                                            "packageType": "2"}, payload)
                    assert success(request("/Packet/Create", "POST", form, token, kind)) is True
                    assert success(request("/Client/Create", "POST", {
                        "appKey": "ordered-ci-app", "notes": "CI"}, token)) == "ordered-ci-app"

                    def new_record():
                        packages = success(request("/Upgrade/Verification", "POST", {
                            "appKey": "ordered-ci-app", "productId": product, "version": "1.0.0",
                            "appType": 1, "platform": 1,
                        }))
                        assert len(packages) == 1 and packages[0]["hash"] == hashlib.sha256(payload).hexdigest()
                        return packages[0]["recordId"]

                    if os.environ.get("ORDERED_REPORT_TERMINAL_CONFIRMED") == "true":
                        def record_snapshot(record_id):
                            with db.cursor() as cursor:
                                cursor.execute("SELECT status,report_time,fault_id FROM tb_upgrade_record "
                                               "WHERE id=%s", (record_id,))
                                state = cursor.fetchone()
                                cursor.execute("SELECT event_id,sequence FROM tb_report_event WHERE record_type=1 "
                                               "AND record_id=%s ORDER BY sequence", (record_id,))
                                events = cursor.fetchall()
                                cursor.execute("SELECT COUNT(*) FROM tb_fault_log WHERE record_type=1 "
                                               "AND record_id=%s", (record_id,))
                                faults = cursor.fetchone()[0]
                            return state, events, faults

                        def terminal_rejection(result):
                            http, body = result
                            assert http == 200 and body["code"] == 400 and body["body"] is False
                            assert body["errorCode"] == "ReportTerminalConflict"

                        legacy_record = new_record()
                        worker("legacy", legacy_record, "legacy")
                        legacy_state = record_snapshot(legacy_record)
                        assert legacy_state[0][0] == 2 and legacy_state[1:] == ((), 0)

                        failure_record = new_record()
                        worker("lost", failure_record, "lost-response")
                        pending_failure = queued("lost-response")
                        assert len(pending_failure) == 1
                        accepted = json.loads(pending_failure[0][1])
                        failure_state = record_snapshot(failure_record)
                        assert failure_state[0][0] == 3 and failure_state[2] == 1
                        assert failure_state[1] == ((accepted["eventId"], 1),)
                        worker("replay", failure_record, "lost-response")
                        assert not queued("lost-response") and record_snapshot(failure_record) == failure_state
                        assert success(request("/Upgrade/Report", "POST", accepted)) is True
                        terminal_rejection(request("/Upgrade/Report", "POST", {
                            "recordId": failure_record, "type": 1, "status": 2,
                            "eventId": str(uuid.uuid4()), "sequence": 2,
                        }))
                        assert record_snapshot(failure_record) == failure_state

                        for mode in ("transient-seed", "unknown-response"):
                            retry_record = new_record()
                            outbox = "retry-" + mode
                            initial = record_snapshot(retry_record)
                            worker(mode, retry_record, outbox)
                            pending = queued(outbox)
                            assert len(pending) == 1 and not quarantined(outbox)
                            event = json.loads(pending[0][1])
                            assert event["recordId"] == retry_record and event["sequence"] == 1
                            assert uuid.UUID(event["eventId"]).int != 0
                            after_failure = record_snapshot(retry_record)
                            if mode == "transient-seed":
                                assert after_failure == initial
                            else:
                                assert after_failure[0][0] == 3 and after_failure[2] == 1
                                assert after_failure[1] == ((event["eventId"], 1),)
                            worker("replay-exact", retry_record, outbox,
                                   hashlib.sha256(pending[0][1].encode("utf-8")).hexdigest())
                            final = record_snapshot(retry_record)
                            assert not queued(outbox) and not quarantined(outbox)
                            assert final[0][0] == 3 and final[2] == 1
                            assert final[1] == ((event["eventId"], 1),)
                            if mode == "unknown-response":
                                assert final == after_failure

                        ordered_record = new_record()
                        newer = {"recordId": ordered_record, "type": 1, "status": 2,
                                 "eventId": str(uuid.uuid4()), "sequence": 4}
                        stale = {"recordId": ordered_record, "type": 1, "status": 3,
                                 "eventId": str(uuid.uuid4()), "sequence": 3}
                        assert success(request("/Upgrade/Report", "POST", newer)) is True
                        final_state = record_snapshot(ordered_record)[0]
                        assert success(request("/Upgrade/Report", "POST", stale)) is True
                        assert success(request("/Upgrade/Report", "POST", stale)) is True
                        ordered_state = record_snapshot(ordered_record)
                        assert ordered_state[0] == final_state and ordered_state[2] == 0
                        assert ordered_state[1] == ((stale["eventId"], 3), (newer["eventId"], 4))
                        terminal_rejection(request("/Upgrade/Report", "POST", {
                            "recordId": ordered_record, "type": 1, "status": 3,
                            "eventId": str(uuid.uuid4()), "sequence": 5,
                        }))
                        assert record_snapshot(ordered_record) == ordered_state

                        terminal_record, fresh_record = new_record(), new_record()
                        assert terminal_record != fresh_record
                        # This suite requires a server candidate explicitly confirmed to reject Success -> Failure.
                        assert success(request("/Upgrade/Report", "POST", {
                            "recordId": terminal_record, "type": 1, "status": 2,
                        })) is True

                        def terminal_snapshot():
                            with db.cursor() as cursor:
                                cursor.execute("SELECT status,report_time,fault_id FROM tb_upgrade_record "
                                               "WHERE id=%s", (terminal_record,))
                                state = cursor.fetchone()
                                cursor.execute("SELECT COUNT(*) FROM tb_report_event WHERE record_type=1 "
                                               "AND record_id=%s", (terminal_record,))
                                events = cursor.fetchone()[0]
                                cursor.execute("SELECT COUNT(*) FROM tb_fault_log WHERE record_type=1 "
                                               "AND record_id=%s", (terminal_record,))
                                faults = cursor.fetchone()[0]
                            return state, events, faults

                        before = terminal_snapshot()
                        assert before[0][0] == 2 and before[1:] == (0, 0)
                        worker("terminal-seed", terminal_record, "terminal")
                        seed = queued("terminal")
                        assert len(seed) == 1 and not quarantined("terminal")
                        original = json.loads(seed[0][1])
                        assert original["recordId"] == terminal_record and original["status"] == 3
                        assert original["sequence"] == 1 and uuid.UUID(original["eventId"]).int != 0
                        worker("terminal-reject", terminal_record, "terminal")
                        retained = quarantined("terminal")
                        assert retained == [(seed[0][0], seed[0][1], "ReportTerminalConflict")]
                        assert not queued("terminal") and terminal_snapshot() == before
                        worker("terminal-restart", terminal_record, "terminal", fresh_record)
                        assert quarantined("terminal") == retained and terminal_snapshot() == before
                        blocked = queued("terminal")
                        assert len(blocked) == 1
                        later = json.loads(blocked[0][1])
                        assert later["recordId"] == terminal_record and later["sequence"] == 2
                        with db.cursor() as cursor:
                            cursor.execute("SELECT status FROM tb_upgrade_record WHERE id=%s", (fresh_record,))
                            assert cursor.fetchone()[0] == 2
                            cursor.execute("SELECT sequence FROM tb_report_event WHERE record_type=1 "
                                           "AND record_id=%s", (fresh_record,))
                            assert cursor.fetchall() == ((1,),)

                        for collision in ("identity", "sequence"):
                            conflict_record = new_record()
                            outbox = "conflict-" + collision
                            worker("terminal-seed", conflict_record, outbox)
                            pending = queued(outbox)
                            assert len(pending) == 1
                            conflicting = dict(json.loads(pending[0][1]), status=1)
                            if collision == "sequence":
                                conflicting["eventId"] = str(uuid.uuid4())
                            assert success(request("/Upgrade/Report", "POST", conflicting)) is True
                            worker("conflict", conflict_record, outbox)
                            assert queued(outbox) == pending and not quarantined(outbox)
                            with db.cursor() as cursor:
                                cursor.execute("SELECT status FROM tb_upgrade_record WHERE id=%s", (conflict_record,))
                                assert cursor.fetchone()[0] == 1
                                cursor.execute("SELECT event_id,sequence FROM tb_report_event WHERE record_type=1 "
                                               "AND record_id=%s", (conflict_record,))
                                assert cursor.fetchall() == ((conflicting["eventId"], 1),)
                                cursor.execute("SELECT COUNT(*) FROM tb_fault_log WHERE record_type=1 "
                                               "AND record_id=%s", (conflict_record,))
                                assert cursor.fetchone()[0] == 0
                        print("Real API candidate: legacy, lost and unknown response, transient transport, replay, "
                              "ordering, terminal quarantine, restart, exact payload, blocked record, "
                              "fresh delivery and ordinary conflicts passed.")
                        return

                    record, other_record, legacy_record = new_record(), new_record(), new_record()
                    assert len({record, other_record, legacy_record}) == 3
                    worker("legacy", legacy_record)
                    with db.cursor() as cursor:
                        cursor.execute("SELECT status FROM tb_upgrade_record WHERE id=%s", (legacy_record,))
                        assert cursor.fetchone()[0] == 2
                        cursor.execute("SELECT COUNT(*) FROM tb_report_event WHERE record_type=1 AND record_id=%s",
                                       (legacy_record,))
                        assert cursor.fetchone()[0] == 0

                    def snapshot():
                        with db.cursor() as cursor:
                            cursor.execute("SELECT event_id,sequence FROM tb_report_event "
                                           "WHERE record_type=1 AND record_id=%s ORDER BY sequence", (record,))
                            events = cursor.fetchall()
                            cursor.execute("SELECT status FROM tb_upgrade_record WHERE id=%s", (record,))
                            status = cursor.fetchone()[0]
                            cursor.execute("SELECT COUNT(*) FROM tb_fault_log WHERE record_type=1 AND record_id=%s",
                                           (record,))
                            count = cursor.fetchone()[0]
                        return events, status, count

                    worker("lost", record)
                    with closing(sqlite3.connect(work / "outbox" / "ordered-reports.db")) as queue:
                        rows = queue.execute("SELECT payload FROM outbox").fetchall()
                    assert len(rows) == 1
                    original = json.loads(rows[0][0])
                    assert original["recordId"] == record and original["sequence"] == 1
                    assert uuid.UUID(original["eventId"]).int != 0
                    events, status, count = snapshot()
                    assert len(events) == 1 and events[0][1] == 1 and status == 3 and count == 1

                    worker("replay", record)  # A separate process opens the same durable outbox.
                    with closing(sqlite3.connect(work / "outbox" / "ordered-reports.db")) as queue:
                        assert queue.execute("SELECT COUNT(*) FROM outbox").fetchone()[0] == 0
                    assert snapshot() == (events, status, count)
                    assert success(request("/Upgrade/Report", "POST", original)) is True
                    assert snapshot() == (events, status, count)

                    worker("success", record)
                    events, status, count = snapshot()
                    assert [row[1] for row in events] == [1, 2] and status == 2 and count == 1
                    later = {"recordId": record, "type": 1, "status": 2,
                             "eventId": str(uuid.uuid4()), "sequence": 4}
                    earlier = {"recordId": record, "type": 1, "status": 3,
                               "eventId": str(uuid.uuid4()), "sequence": 3,
                               "category": 3, "phase": "Apply", "message": "Stale failure"}
                    assert success(request("/Upgrade/Report", "POST", later)) is True
                    assert success(request("/Upgrade/Report", "POST", earlier)) is True
                    events, status, count = snapshot()
                    assert [row[1] for row in events] == [1, 2, 3, 4] and status == 2 and count == 1
                    conflicts = [dict(original, status=2),
                                 dict(original, eventId=str(uuid.uuid4())),
                                 dict(original, recordId=other_record)]
                    for conflict in conflicts:
                        http, result = request("/Upgrade/Report", "POST", conflict)
                        assert http == 200 and result["code"] == 400
                        assert result["errorCode"] == "Conflict" and result["body"] is False
                        assert snapshot() == (events, status, count)
                    print("Real API: legacy report, lost response, process restart, deduplication, ordering and conflicts passed.")
            finally:
                process.terminate()
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)


if __name__ == "__main__":
    main()
