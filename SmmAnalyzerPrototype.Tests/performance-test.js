import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
    insecureSkipTLSVerify: true,
    scenarios: {
        base_load_test: {
            executor: "constant-vus",
            vus: 10,
            duration: "1m"
        }
    },
    thresholds: {
        http_req_failed: ["rate<0.05"],
        http_req_duration: ["p(95)<2000"]
    }
};

const BASE_URL = __ENV.BASE_URL || "https://localhost:7001";

function uniqueValue(prefix) {
    return `${prefix}_${Date.now()}_${__VU}_${__ITER}_${Math.floor(Math.random() * 1000000)}`;
}

function jsonHeaders(userId) {
    const headers = {
        "Content-Type": "application/json"
    };

    if (userId) {
        headers["X-User-Id"] = userId;
    }

    return headers;
}

export default function () {
    const login = uniqueValue("perf_user");
    const email = `${login}@example.com`;
    const password = "1234567";

    const registerPayload = JSON.stringify({
        login: login,
        email: email,
        password: password
    });

    const registerResponse = http.post(
        `${BASE_URL}/api/accountapi/register`,
        registerPayload,
        {
            headers: jsonHeaders()
        }
    );

    const registerOk = check(registerResponse, {
        "registration status is 200": (response) => response.status === 200,
        "registration response has user id": (response) => {
            try {
                return Boolean(response.json("id"));
            } catch {
                return false;
            }
        }
    });

    if (!registerOk) {
        return;
    }

    const userId = registerResponse.json("id");

    const communityPayload = JSON.stringify({
        name: uniqueValue("Тестовое сообщество"),
        targetAudience: "Студенты и молодые специалисты",
        styleProfile: "Дружелюбный информационный стиль",
        vkInput: null
    });

    const createCommunityResponse = http.post(
        `${BASE_URL}/api/communityapi/create`,
        communityPayload,
        {
            headers: jsonHeaders(userId)
        }
    );

    const communityOk = check(createCommunityResponse, {
        "community create status is 201": (response) => response.status === 201,
        "community response has id": (response) => {
            try {
                return Boolean(response.json("id"));
            } catch {
                return false;
            }
        }
    });

    if (!communityOk) {
        return;
    }

    const communityId = createCommunityResponse.json("id");

    const postPayload = JSON.stringify({
        communityId: communityId,
        text: "Тестовая публикация для проверки производительности системы анализа постов."
    });

    const createPostResponse = http.post(
        `${BASE_URL}/api/postapi/create`,
        postPayload,
        {
            headers: jsonHeaders(userId)
        }
    );

    const postOk = check(createPostResponse, {
        "post create status is 201": (response) => response.status === 201,
        "post response has id": (response) => {
            try {
                return Boolean(response.json("id"));
            } catch {
                return false;
            }
        }
    });

    if (!postOk) {
        return;
    }

    const postId = createPostResponse.json("id");

    const getCommunitiesResponse = http.get(
        `${BASE_URL}/api/communityapi/getall`,
        {
            headers: {
                "X-User-Id": userId
            }
        }
    );

    check(getCommunitiesResponse, {
        "community list status is 200": (response) => response.status === 200,
        "community list is not empty": (response) => {
            try {
                const data = response.json();
                return Array.isArray(data) && data.length > 0;
            } catch {
                return false;
            }
        }
    });

    const getPostsResponse = http.get(
        `${BASE_URL}/api/postapi/getall`,
        {
            headers: {
                "X-User-Id": userId
            }
        }
    );

    check(getPostsResponse, {
        "post list status is 200": (response) => response.status === 200,
        "post list is not empty": (response) => {
            try {
                const data = response.json();
                return Array.isArray(data) && data.length > 0;
            } catch {
                return false;
            }
        }
    });

    const getPostDetailsResponse = http.get(
        `${BASE_URL}/api/postapi/getbyid/${postId}`,
        {
            headers: {
                "X-User-Id": userId
            }
        }
    );

    check(getPostDetailsResponse, {
        "post details status is 200": (response) => response.status === 200,
        "post details contains text": (response) => {
            try {
                return response.json("text") === "Тестовая публикация для проверки производительности системы анализа постов.";
            } catch {
                return false;
            }
        }
    });

    sleep(1);
}